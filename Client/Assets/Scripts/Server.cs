using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using DisruptorUnity3d;
using ENet;
using Lockstep.Logic;
using Lockstep.Util;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;

public class Server
{

    private Host m_HostServer;
    private Thread m_NetThread;
    private Address m_Address;
    private readonly RingBuffer<EServerNetThreadRequest> m_Requests = new RingBuffer<EServerNetThreadRequest>(8);
    private readonly RingBuffer<EServerNetThreadResponse> m_Responses = new RingBuffer<EServerNetThreadResponse>(8);
    private readonly RingBuffer<FDisconnectData> m_DisconnectData = new RingBuffer<FDisconnectData>(128);
    private readonly RingBuffer<FReceivedEvent> m_ReceivedEvents = new RingBuffer<FReceivedEvent>(1024);
    private readonly RingBuffer<FSendData> m_SendData = new RingBuffer<FSendData>(1024);
    
    private readonly PacketFreeCallback m_FreeCallback = packet => { Marshal.FreeHGlobal(packet.Data); };
    private readonly IntPtr             m_CachedFreeCallback;

    
    private ServerCommandHandler m_CommandHandler;

    public EServerState State = EServerState.Stopped;
    //@TODO: 封装到房间中
    public readonly Dictionary<uint, Peer> Id2ConnectedPeers = new Dictionary<uint, Peer>();
    public Dictionary<uint, PlayerServerInfo> Id2PlayerInfos = new Dictionary<uint, PlayerServerInfo>();
    public Dictionary<string, PlayerServerInfo> Name2PlayerInfos = new Dictionary<string, PlayerServerInfo>();
    private bool m_GameStarted = false;
    
    private Dictionary<int, PlayerInput[]> m_Tick2Inputs = new Dictionary<int, PlayerInput[]>();
    private Dictionary<int, int[]> m_Tick2Hashes = new Dictionary<int, int[]>();
    
    
    


    #region 游戏/Game

    private float _gameFirstFrameTimeStamp = 0;

    public int _tickSinceGameStart =>
        (int)((Time.time - _gameStartTimestampMs) / GameEntry.Instance.ServerStepInterval);
        //(int) ((LTime.realtimeSinceStartupMS - _gameStartTimestampMs) / GameEntry.Instance.StepIntervalMS);
    
    public float _gameStartTimestampMs = -1;

    private int m_CurrentTick;
    

    #endregion

    #region 非游戏/Server
    

    private DateTime _lastUpdateTimeStamp;
    

    #endregion
    //private int _ServerTickDelay;


    public Server()
    {
        m_CommandHandler = new ServerCommandHandler(this);
        m_CachedFreeCallback = Marshal.GetFunctionPointerForDelegate(m_FreeCallback);

    }

    public void CancelStartServer()
    {
        if (State != EServerState.Starting)
        {
            Debug.Log($"CancelStartServer fail, current state is {State}");
            return;
        }

        Debug.Log("CancelStartServer");
        State = EServerState.Stopping;
        EnqueueRequest(EServerNetThreadRequest.Stop);
    }

    public void StopServer()
    {
        if (State != EServerState.Working)
        {
            Debug.Log($"StopServer fail, current state is {State}");
            return;
        }

        Debug.Log("Stop Server");
        State = EServerState.Stopping;

        foreach (var connectedPeer in Id2ConnectedPeers.Values)
        {
            m_DisconnectData.Enqueue(new FDisconnectData()
            {
                Peer = connectedPeer
            });
        }

        EnqueueRequest(EServerNetThreadRequest.Stop);
    }

    public void ClearBuffers()
    {
        //@TODO: 清理服务器相关数据
        Id2ConnectedPeers.Clear();
    }

    public void ServerStartGame()
    {
        OnStartGame(new Msg_StartGame());
    }
    
    public void OnStartGame(IMessage message)
    {
        Debug.Log("Server StartGame");
        m_GameStarted = true;

        Msg_StartGame msgStartGame = message as Msg_StartGame;
        //@TODO:
        msgStartGame.mapId = 0;
        msgStartGame.playerInfos = Id2PlayerInfos.Values.ToArray();

        var bytes = msgStartGame.ToBytes();
        
        Debug.Log($"Server broadcast startGame, size(totalSize - 4): {bytes.Length}");

        int playerLocalId = 0;
        foreach (var playerServerInfo in Id2PlayerInfos.Values)
        {
            //@TODO:
            ++playerLocalId;
            msgStartGame.localPlayerId = playerLocalId; 
            var newPtr = MessagePacker.Instance.GetBytesPtr(msgStartGame.OpCode, bytes, out var totalLength);

            FSendData sendData = new FSendData()
            {
                Data = newPtr,
                Length = totalLength,
                Peer = sendData.Peer = Id2ConnectedPeers[playerServerInfo.id]
            };
            EnqueueSendData(sendData);
        }
        
        //@TODO: endgame之后要设置m_GameStarted为false等
    }
    
    
    public void OnPlayerInput(Peer peer, IMessage message)
    {
        Msg_PlayerInput msgPlayerInput = message as Msg_PlayerInput;

        PlayerServerInfo playerInfo = Id2PlayerInfos[peer.ID];
        PlayerInput[] playerInputs = null;
        if (!m_Tick2Inputs.TryGetValue(msgPlayerInput.Tick, out playerInputs))
        {
            //当前玩家所有输入而不是所有玩家
            playerInputs = new PlayerInput[Id2PlayerInfos.Count];
            m_Tick2Inputs.Add(msgPlayerInput.Tick, playerInputs);
        }

        playerInputs[playerInfo.id] = msgPlayerInput.PlayerInput;
        CheckInput(false);
    }

    public void OnPlayerHash(Peer peer, IMessage message)
    {
        Msg_PlayerHash msgPlayerHash = message as Msg_PlayerHash;
        
        int[] hashes;
        if (!m_Tick2Hashes.TryGetValue(msgPlayerHash.Tick, out hashes))
        {
            hashes = new int[Id2PlayerInfos.Count];
            m_Tick2Hashes.Add(msgPlayerHash.Tick, hashes);
        }
        //@TODO: 先获取localid然后进行存入
        hashes[peer.ID] = msgPlayerHash.Hash;
        
        //hash还没全部到齐，退出检查
        foreach (int hash in hashes)
        {
            if (hash == 0)
                return;
        }

        bool checkPass = true;
        int compareValue = hashes[0];
        foreach (int hash in hashes)
        {
            if (hash != compareValue)
            {
                checkPass = false;
                break;
            }
        }

        if (false == checkPass)
        {
            Debug.Log(msgPlayerHash.Tick + " Hash is different " + compareValue);
        }

        m_Tick2Hashes.Remove(msgPlayerHash.Tick);
    }

    private void OnStartServerSuccess()
    {
        
    }

    
    public void ExecuteNet(float deltaTime)
    {
        while (m_Responses.TryDequeue(out var response))
            switch (response)
            {
                case EServerNetThreadResponse.StartSuccess:
                    Debug.Log("Server is working");
                    State = EServerState.Working;
                    OnStartServerSuccess();
                    break;
                case EServerNetThreadResponse.StartFailure:
                    Debug.Log("Server start failed");
                    State = EServerState.Stopped;
                    GameEntry.OnHostFinish();
                    m_NetThread?.Abort();
                    m_NetThread = null;
                    break;
                case EServerNetThreadResponse.Stoppoed:
                    Debug.Log("Server is stopped");
                    GameEntry.OnHostFinish();

                    ClearBuffers();
                    while (m_ReceivedEvents.TryDequeue(out _))
                    {
                    }

                    State = EServerState.Stopped;
                    m_NetThread?.Abort();
                    m_NetThread = null;
                    break;
            }

        if (State != EServerState.Working) return;
        while (m_ReceivedEvents.TryDequeue(out var @event))
        {
            switch (@event.EventType)
            {
                case EventType.Connect:
                    m_CommandHandler.OnClientConnected(@event.Peer);
                    break;
                case EventType.Disconnect:
                    m_CommandHandler.OnClientDisconnected(@event.Peer);
                    break;
                case EventType.Receive:
                    // var currentPeerId = (ushort)@event.Peer.ID;
                    // m_CommandHandler.CurrentClientPeerID = currentPeerId;
                    //所有消息处理入口，游戏开始，结束，加入房间，输入..
                    m_CommandHandler.OnReceive(ref @event);
                    break;
                case EventType.Timeout:
                    m_CommandHandler.OnClientDisconnected(@event.Peer);
                    break;
            }
        }
        

    }

    public void Update(float deltaTime)
    {
        OnUpdate(deltaTime);
    }
    public void OnUpdate(float deltaTime)
    {
        if (false == m_GameStarted)
            return;
        
        while (m_CurrentTick < _tickSinceGameStart)
        {
            CheckInput(true);
        }
    }
    
    private void CheckInput(bool force)
    {
        if (false == m_GameStarted)
            return;

        PlayerInput[] playerInputs = null;
        if (false == m_Tick2Inputs.ContainsKey(m_CurrentTick))
        {
            playerInputs = new PlayerInput[Id2PlayerInfos.Count];
            m_Tick2Inputs[m_CurrentTick] = playerInputs;
        }

        playerInputs = m_Tick2Inputs[m_CurrentTick];

        bool isFullInput = true;
        for (int i = 0; i < playerInputs.Length; ++i)
        {
            if (playerInputs[i] == null)
            {
                isFullInput = false;
                break;
            }
        }

        if (false == isFullInput && false == force)
        {
            return;
        }

        //如果输入满了或者force，那么直接发送，目前序列化会记录是否是null

        BoardInputMsg(m_CurrentTick, playerInputs);
        m_Tick2Inputs.Remove(m_CurrentTick);
        ++m_CurrentTick;

        //if (isFullInput)
        // {
        //     BoardInputMsg(m_CurrentTick, playerInputs);
        //     m_Tick2Inputs.Remove(m_CurrentTick);
        //     ++m_CurrentTick;
        // }

        
        if (_gameFirstFrameTimeStamp <= 0) {
            _gameFirstFrameTimeStamp = Time.time;
        }

        if (_gameStartTimestampMs < 0)
        {
            _gameStartTimestampMs = Time.time;
        }
        
        
    }
    private void BoardInputMsg(int tick, PlayerInput[] inputs){
        var frame = new Msg_FrameInput();
        frame.Input = new FrameInput() {
            tick = tick,
            inputs = inputs
        };
        
        var bytes = frame.ToBytes();
        Debug.Log($"server broad input, size(totalSize - 4): {bytes.Length}");

        foreach (var playerServerInfo in Id2PlayerInfos.Values)
        {
            IntPtr data = MessagePacker.Instance.GetBytesPtr(frame.OpCode, bytes, out var totalLength);
            FSendData sendData = new FSendData()
            {
                Data = data,
                Length = totalLength,
                Peer = Id2ConnectedPeers[playerServerInfo.id]
            };
            EnqueueSendData(sendData);
        }

    }
    
    private void NetworkThread()
    {
        while (true)
        {
            while (m_SendData.TryDequeue(out var data))
            {
                var packet = new Packet();
                packet.Create(data.Data, data.Length, PacketFlags.Reliable | PacketFlags.NoAllocate);
                packet.SetFreeCallback(m_CachedFreeCallback);
                data.Peer.Send(0, ref packet);
            }
            
            while (m_DisconnectData.TryDequeue(out var data)) data.Peer.DisconnectNow(0);

            while (m_Requests.TryDequeue(out var request))
                switch (request)
                {
                    case EServerNetThreadRequest.Start:
                        try
                        {
                            m_HostServer.Create(m_Address, GameEntry.Instance.MaxPeers, GameEntry.Instance.MaxChannels);
                            //m_HostServer.Create();
                            m_Responses.Enqueue(EServerNetThreadResponse.StartSuccess);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e.Message);
                            m_HostServer = new Host();
                            m_Responses.Enqueue(EServerNetThreadResponse.StartFailure);
                        }

                        break;
                    case EServerNetThreadRequest.Stop:
                        m_HostServer.Flush();
                        m_HostServer.Dispose();
                        m_HostServer = new Host();
                        m_Responses.Enqueue(EServerNetThreadResponse.Stoppoed);
                        break;
                }


            Event netEvent;
            bool polled = false;
            if (!m_HostServer.IsSet)
                Thread.Sleep(15);
            else
                while (!polled)
                {
                    if (m_HostServer.CheckEvents(out netEvent) <= 0)
                    {
                        if (m_HostServer.Service(15, out netEvent) <= 0)
                            break;

                        polled = true;
                    }

                    switch (netEvent.Type)
                    {
                        case EventType.None:
                            Debug.LogError("EventType is none");
                            break;

                        case EventType.Connect:
                            Debug.Log("Client connected - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);
                            m_ReceivedEvents.Enqueue(new FReceivedEvent
                                { EventType = @netEvent.Type, Peer = netEvent.Peer });
                            break;

                        case EventType.Disconnect:
                            Debug.Log("Client disconnected - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);
                            m_ReceivedEvents.Enqueue(new FReceivedEvent
                                { EventType = @netEvent.Type, Peer = netEvent.Peer });
                            break;

                        case EventType.Timeout:
                            Debug.Log("Client timeout - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);
                            m_ReceivedEvents.Enqueue(new FReceivedEvent
                                { EventType = @netEvent.Type, Peer = netEvent.Peer });
                            break;

                        case EventType.Receive:
                            //Debug.LogError("Receive EventType not implement");
                            Debug.Log("Packet received from - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP +
                                      ", Channel ID: " + netEvent.ChannelID + ", Data length: " +
                                      netEvent.Packet.Length);
                            
                            unsafe
                            {
                                var length = @netEvent.Packet.Length;
                                var newPtr = Marshal.AllocHGlobal(length);
                                Buffer.MemoryCopy(@netEvent.Packet.Data.ToPointer(), newPtr.ToPointer(), length,
                                    length);
                                m_ReceivedEvents.Enqueue(new FReceivedEvent()
                                {
                                    Data = newPtr,
                                    EventType = EventType.Receive,
                                    Peer = netEvent.Peer
                                });
                            }
                            
                            netEvent.Packet.Dispose();
                            break;
                    }
                }
        }
    }

    public void EnqueueRequest(EServerNetThreadRequest request)
    {
        m_Requests.Enqueue(request);
    }

    public void EnqueueDisconnectData(FDisconnectData data)
    {
        m_DisconnectData.Enqueue(data);
    }
    public void EnqueueSendData(FSendData data)
    {
        m_SendData.Enqueue(data);
    }
    

    public Thread StartServer(string ip, ushort port)
    {
        m_HostServer = new Host();
        m_Address = new Address()
        {
            Port = port
        };

        State = EServerState.Starting;
        //
        //address.SetHost(ip);
        //m_HostServer.Create(m_Address, MaxPlayers);
        EnqueueRequest(EServerNetThreadRequest.Start);

        m_NetThread = new Thread(NetworkThread);
        m_NetThread.Start();

        
        return m_NetThread;
    }
}