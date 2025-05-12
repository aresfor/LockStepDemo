using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using DisruptorUnity3d;
using ENet;
using Lockstep.Logic;
using Lockstep.Math;
using Lockstep.Util;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;

public class Client
{
    private Thread m_NetThread;
    private Host m_HostClient;
    private Address m_Address;
    private readonly RingBuffer<EClientNetThreadResponse> m_Responses = new RingBuffer<EClientNetThreadResponse>(8);
    private readonly RingBuffer<EClientNetThreadRequest> m_Requests = new RingBuffer<EClientNetThreadRequest>(8);
    private readonly RingBuffer<FReceivedEvent> m_ReceivedEvents = new RingBuffer<FReceivedEvent>(1024);
    private readonly RingBuffer<FSendData>              m_SendData       = new RingBuffer<FSendData>(1024);
    private readonly PacketFreeCallback m_FreeCallback = packet => { Marshal.FreeHGlobal(packet.Data); };
    private readonly IntPtr             m_CachedFreeCallback;
    
    
    private readonly ClientCommandHandler m_CommandHandler;
    public Peer ConnectedServerPeer;
    public EClientState State = EClientState.Disconnected;
    
    

    public int PreSendInputCount { get; private set; } = 2;
    public int InputTick { get; private set; } = 0;
    public int InputTargetTick => m_TickSinceGameStart + PreSendInputCount;
    
    
    /// game init timestamp
    private long m_GameStartTimestampMs = -1;

    private int m_TickSinceGameStart;
    /// frame count that need predict(TODO should change according current network's delay)，这个和framebuffer中的maxclientpredictframecount不一样，两边单独分开判断
    public int FramePredictCount = 0;//~~~
    public int TargetTick => m_TickSinceGameStart + FramePredictCount;

    #region 房间/游戏变量

    //@TODO：封装到房间中
    public PlayerServerInfo[] PlayerServerInfos;
    public Player[] Players;
    
    //public List<FrameInput> Frames = new List<FrameInput>();
    
    //服务器发送开始游戏的时候，每个玩家的localid都是不同的，具体对应服务器上玩家数组的索引
    private int m_LocalPlayerId;
    
    private bool m_GameStarted = false;

    #endregion

    private FrameBuffer m_FrameBuffer;
    
    public Client()
    {
        m_CommandHandler = new ClientCommandHandler(this);
        m_CachedFreeCallback = Marshal.GetFunctionPointerForDelegate(m_FreeCallback);
    }

    public Thread StartClient(Host host, Address address, int bufferCapacity)
    {
        m_HostClient = host;
        m_HostClient.Create();

        m_FrameBuffer = new FrameBuffer(bufferCapacity);
        
        m_Address = address;
        State = EClientState.Connecting;
        EnqueueRequest(EClientNetThreadRequest.Connect);
        m_NetThread = new Thread(NetworkThread);
        m_NetThread.Start();

        return m_NetThread;
    }

    public void EnqueueRequest(EClientNetThreadRequest request)
    {
        m_Requests.Enqueue(request);
    }
    
    public void EnqueueSendData(FSendData data)
    {
        m_SendData.Enqueue(data);
    }

    public void CancelConnect2Server()
    {
        if (State != EClientState.Connecting)
        {
            Debug.Log($"CancelConnect2Server fail, current state is {State}");
            return;
        }
        Debug.Log("CancelConnect2Server");
        //State = EClientState.Disconnecting;
        EnqueueRequest(EClientNetThreadRequest.CancelConnect);
    }
    public void StopClient()
    {
        if (State != EClientState.Connected)
        {
            Debug.Log($"StopClient fail, current state is {State}");
            return;
        }

        Debug.Log("StopClient");
        State = EClientState.Disconnecting;
        EnqueueRequest(EClientNetThreadRequest.Disconnect);
    }

    public void CleanupState()
    {
        while (m_ReceivedEvents.TryDequeue(out _))
        {
        }

        //while (_states.Count > 0) Marshal.FreeHGlobal(_states.Dequeue());

        State = EClientState.Disconnected;
        ConnectedServerPeer = new Peer();
        //ConnectionId.IsSet = false;
        //_firstPacket       = true;

        //_syncGroup.GetEntities(_syncBuffer);
        //foreach (var e in _syncBuffer) e.isDestroyed = true;

        //EnqueuedCommandCount = 0;
        //ToServer.Clear();
        //_fromServer.Clear();
    }

    public void OnUpdate(float deltaTime)
    {
        m_TickSinceGameStart =
            (int) ((LTime.realtimeSinceStartupMS - m_GameStartTimestampMs)
                   / GameEntry.Instance.UpdateCallPerSecond);

        m_FrameBuffer.OnUpdate(deltaTime, GameEntry.CurrentTick);
        
        if (GameEntry.Instance.IsClientMode)
        {
            OnUpdateClientMode();
        }
        else
        {
            while (InputTick <= InputTargetTick)
            {
                SendInputs(InputTick);
                ++InputTick;
            }
            OnUpdateInternal();
        }
    }

    private void SendInputs(int tick)
    {
        //@TODO: 获取本地的完整输入，这里只是临时的，输入需要通过inputservice去获取
        
        //@TODO: pool
        Msg_FrameInput msgframeInput = new Msg_FrameInput()
        {
            Input = new FrameInput()
            {
                inputs = new[]
                {
                    GameEntry.CurrentGameInput.Clone()
                },
                tick = tick
            }
        };

        StepFrame stepFrame = new StepFrame(msgframeInput);
        FillInputWithLastFrame(stepFrame);
        m_FrameBuffer.EnqueueLocalFrame(stepFrame);
        
        //@TODO: 合并过去历史的输入为一个input包
        if (tick > m_FrameBuffer.MaxServerTickInBuffer)
        {
            GameEntry.Instance.Tick2SendTimer[tick] = LTime.realtimeSinceStartupMS;
            SendInput2Server();

        }
    }

    public void FillInputWithLastFrame(StepFrame stepFrame)
    {
        int tick = stepFrame.Tick;
        StepFrame lastFrame = m_FrameBuffer.GetFrame(tick - 1);
        PlayerInput localPlayerInput = stepFrame.FrameInput.Input.inputs[m_LocalPlayerId];
        for (int i = 0; i < Players.Length; ++i)
        {
            stepFrame.FrameInput.Input.inputs[i] = lastFrame.FrameInput.Input.inputs[i].Clone();
        }

        stepFrame.FrameInput.Input.inputs[m_LocalPlayerId] = localPlayerInput;
        
    }

    private void FillInputWithLastFrame(FrameInput frame){
        // int tick = frame.tick;
        // var inputs = frame.inputs;
        // var lastServerInputs = tick == 0 ? null : m_CommandBuffer.GetFrame(tick - 1)?.Inputs;
        // var localInput = inputs[m_LocalPlayerId];
        // //fill inputs with last frame's input (Input predict)
        // for (int i = 0; i < Players.Length; i++) {
        //     inputs[i] = new Msg_PlayerInput(tick, _allActors[i], lastServerInputs?[i]?.Commands);
        // }
        //
        // inputs[m_LocalPlayerId] = localInput;
    }
    
    private void OnUpdateClientMode()
    {
        while (GameEntry.CurrentTick < TargetTick)
        {
            //FramePredictCount = 0;
            Msg_PlayerInput msgPlayerInput = new Msg_PlayerInput()
            {
                PlayerInput = GameEntry.CurrentGameInput.Clone(),
                Tick = GameEntry.CurrentTick
            };
            Msg_FrameInput msgFrameInput = new Msg_FrameInput()
            {
                Input = new FrameInput()
                {
                    inputs = new[] { msgPlayerInput.PlayerInput },
                    tick = GameEntry.CurrentTick
                }
            };
            
            //@TODO: Push frame...
        }
    }

    private void OnUpdateInternal()
    {
        
    }
    
    public void Execute(float deltaTime)
    {
        if (!m_GameStarted)
            return;
        
        //@TODO: debug rollback to ...
        

        if (!GameEntry.Instance.IsReplayMode)
        {
            SendInput2Server();
        }
        
        if (GetFrame(GameEntry.CurrentTick) == null)
            return;

        Step(deltaTime);
    }

    public void Step(float deltaTime)
    {
        UpdateFrameInput();
        
        //@TODO: Replay JumpTp
        if (GameEntry.Instance.IsReplayMode)
        {
            if (GameEntry.CurrentTick < Frames.Count)
            {
                Replay(deltaTime);
                ++GameEntry.CurrentTick;
                int nextClientTick = GameEntry.CurrentTick;
                m_FrameBuffer.SetNextClientTick(nextClientTick);
            }
        }
        else
        {
            //send hash
            StepInternal(deltaTime);

            SendHash2Server();
            ++GameEntry.CurrentTick;
            int nextClientTick = GameEntry.CurrentTick;
            m_FrameBuffer.SetNextClientTick(nextClientTick);
        }
    }

    private void SendHash2Server()
    {
        Msg_PlayerHash playerHash = new Msg_PlayerHash();
        playerHash.Hash = GetHash();
        playerHash.Tick = GameEntry.CurrentTick;
        var bytes = playerHash.ToBytes();
        var data = MessagePacker.Instance.GetBytesPtr(playerHash.OpCode, bytes, out var totalLength);
        
        EnqueueSendData(new FSendData()
        {
            Data = data,
            Length = totalLength,
            Peer =  ConnectedServerPeer
        });
    }

    private int GetHash()
    {
        int hash = 1;
        int idx = 0;
        
        
        foreach (var entity in Players) {
            hash += entity.LocalId.GetHash() * PrimerLUT.GetPrimer(idx++);
            hash += entity.Position.GetHash() * PrimerLUT.GetPrimer(idx++);
        }

        // foreach (var entity in EnemyManager.Instance.allEnemy) {
        //     hash += entity.currentHealth.GetHash() * PrimerLUT.GetPrimer(idx++);
        //     hash += entity.transform.GetHash() * PrimerLUT.GetPrimer(idx++);
        // }

        return hash;
    }


    private void Replay(float deltaTime)
    {
        StepInternal(deltaTime);
    }

    private void StepInternal(float deltaTime)
    {
        LFloat deltaTimeF = deltaTime.ToLFloat();

        foreach (var player in Players)
        {
            var uv = player.Input.InputUV;
            player.Position += new LVector3(uv.x,LFloat.zero, uv.y) * deltaTimeF;
            player.Go.transform.position = player.Position.ToVector3();
        }
        
        
    }
    
    private void UpdateFrameInput(){
        var curFrameInput = GetFrame(GameEntry.CurrentTick);
        for (int i = 0; i < Players.Length; i++) {
            Players[i].Input = curFrameInput.inputs[i];
        }
        
    }
    public FrameInput GetFrame(int tick){
        if (Frames.Count > tick) {
            var frame = Frames[tick];
            if (frame != null && frame.tick == tick) {
                return frame;
            }
        }

        return null;
    }

    public void SendStartGame2Server()
    {
        Debug.Log("client send startGame to server");
        Msg_StartGame msgStartGame = new Msg_StartGame();
        
        var bytes = msgStartGame.ToBytes();
        var data = MessagePacker.Instance.GetBytesPtr(msgStartGame.OpCode, bytes, out var totalLength);

        EnqueueSendData(new FSendData()
        {
            Data = data,
            Length = totalLength,
            Peer = ConnectedServerPeer
        });
    }

    
    //@TODO: 封装到net service中
    public void SendInput2Server(StepFrame stepFrame)
    {
        var bytes = stepFrame.FrameInput.ToBytes();
        var data = MessagePacker.Instance.GetBytesPtr(stepFrame.FrameInput.OpCode, bytes, out var totalLength);
        EnqueueSendData(new FSendData()
        {
            Data = data,
            Length = totalLength,
            Peer =  ConnectedServerPeer
        });

        GameEntry.Instance.Tick2SendTimer[InputTick] = Time.realtimeSinceStartup;
        ++InputTick;
    }

    public void StartGame(IMessage message)
    {
        Debug.Log("client startGame!");
        
        Msg_StartGame msgStartGame = message as Msg_StartGame;

        PlayerServerInfos = msgStartGame.playerInfos;

        m_LocalPlayerId = msgStartGame.localPlayerId; 

        StartGameInternal();
        //@TODO:
        //var mapId =msgStartGame.mapId
        //@TODO: endgame之后要设置m_GameStarted为false等
    }

    public void StartGameInternal()
    {
        m_GameStarted = true;
        
        Players = new Player[PlayerServerInfos.Length];
        for(int i = 0;i<PlayerServerInfos.Length;++i)
        {
            Players[i] = new Player()
            {
                LocalId = PlayerServerInfos[i].localId
            };
        }
        
        //生成实体
        for (int i = 0; i < Players.Length; ++i)
        {
            var newPlayerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            newPlayerGo.name = "Player_" + Players[i].LocalId.ToString();
            Players[i].Go = newPlayerGo;
        }
        
        //@TODO: 本地模式实际上应该是模型加载完，关卡加载完之后再初始化时间
        //，而联机模式应该是等服务器第一个帧下发，放在了OnFrameInput中
        if (GameEntry.Instance.IsClientMode)
        {
            m_GameStartTimestampMs = LTime.realtimeSinceStartupMS;
        }
        else
        {
            while (InputTick < PreSendInputCount)
            {
                SendInputs(InputTick);
                ++InputTick;
            }
        }
        
    }
    
    
    
    public void OnFrameInput(IMessage message)
    {
        Msg_FrameInput msgFrameInput = message as Msg_FrameInput;

        FrameInput input = msgFrameInput.Input;


        //@TODO: pool
        StepFrame stepFrame = new StepFrame(msgFrameInput);

        m_FrameBuffer.EnqueueServerFrame(stepFrame);
        
        //如果是第一次收到服务器帧，记录时间作为游戏开始时间
        if (m_GameStartTimestampMs == -1) {
            m_GameStartTimestampMs = LTime.realtimeSinceStartupMS;
        }
    }
    
    // 在游戏正常逻辑Execute之前
    public void UpdateNetwork()
    {
        while (m_Responses.TryDequeue(out var response))
            switch (response)
            {
                case EClientNetThreadResponse.ConnectFailure:
                    Debug.Log("Connect failure");
                    State = EClientState.Disconnected;
                    GameEntry.OnHostFinish();
                    m_NetThread?.Abort();
                    m_NetThread = null;
                    break;
                case EClientNetThreadResponse.Disconnected:
                    Debug.Log("Disconnected");
                    GameEntry.OnHostFinish();
                    CleanupState();
                    m_NetThread?.Abort();
                    m_NetThread = null;
                    break;
                case EClientNetThreadResponse.ConnectCancelled:
                    Debug.Log("Connect cancelled");
                    GameEntry.OnHostFinish();
                    CleanupState();
                    m_NetThread?.Abort();
                    m_NetThread = null;
                    break;
            }

        if (State == EClientState.Disconnected) return;
        while (m_ReceivedEvents.TryDequeue(out var @event))
            switch (@event.EventType)
            {
                case EventType.Connect:
                    m_CommandHandler.OnConnected(@event.Peer);
                    break;
                case EventType.Disconnect:
                    m_CommandHandler.OnDisconnected(@event.Peer);
                    break;
                case EventType.Receive:
                    m_CommandHandler.OnReceive(@event.Data);
                    break;
                case EventType.Timeout:
                    m_CommandHandler.OnDisconnected(@event.Peer);
                    break;
                default:
                    Debug.LogError("Missing Case");
                    break;
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
            
            while (m_Requests.TryDequeue(out var request))
                switch (request)
                {
                    case EClientNetThreadRequest.Connect:
                        try
                        {
                            m_HostClient.Connect(m_Address);
                        }
                        catch (Exception e)
                        {
                            Debug.Log(e.Message);
                            m_HostClient = new Host();
                            m_HostClient.Create();
                            m_Responses.Enqueue(EClientNetThreadResponse.ConnectFailure);
                        }

                        break;
                    case EClientNetThreadRequest.Disconnect:
                        ConnectedServerPeer.DisconnectNow(0);
                        m_HostClient.Flush();
                        m_HostClient.Dispose();
                        m_HostClient = new Host();
                        m_HostClient.Create();
                        m_Responses.Enqueue(EClientNetThreadResponse.Disconnected);
                        break;
                    case EClientNetThreadRequest.DisconnectFromServer:
                        m_HostClient = new Host();
                        m_HostClient.Create();
                        m_Responses.Enqueue(EClientNetThreadResponse.Disconnected);
                        break;
                    case EClientNetThreadRequest.CancelConnect:
                        m_HostClient.Dispose();
                        m_HostClient = new Host();
                        m_HostClient.Create();
                        m_Responses.Enqueue(EClientNetThreadResponse.ConnectCancelled);
                        break;
                }


            Event netEvent;
            bool polled = false;
            if (!m_HostClient.IsSet)
                Thread.Sleep(15);
            else
                while (!polled)
                {
                    if (m_HostClient.CheckEvents(out netEvent) <= 0)
                    {
                        if (m_HostClient.Service(15, out netEvent) <= 0)
                            break;

                        polled = true;
                    }

                    switch (netEvent.Type)
                    {
                        case EventType.None:
                            Debug.LogError("NetEvent Type is none");
                            break;

                        case EventType.Connect:
                            Debug.Log("Client connected to server");
                            m_ReceivedEvents.Enqueue(new FReceivedEvent()
                            {
                                EventType = netEvent.Type,
                                Peer = netEvent.Peer,
                            });
                            break;

                        case EventType.Disconnect:
                            Debug.Log("Client disconnected from server");
                            m_ReceivedEvents.Enqueue(new FReceivedEvent()
                            {
                                EventType = netEvent.Type,
                                Peer = netEvent.Peer,
                            });
                            break;

                        case EventType.Timeout:
                            Debug.Log("Client connection timeout");
                            m_ReceivedEvents.Enqueue(new FReceivedEvent()
                            {
                                EventType = netEvent.Type,
                                Peer = netEvent.Peer,
                            });
                            break;

                        case EventType.Receive:
                            Debug.Log("Packet received from server - Channel ID: " + netEvent.ChannelID +
                                      ", Data length: " + netEvent.Packet.Length);
                            
                            unsafe
                            {
                                var length = @netEvent.Packet.Length;
                                var newPtr = Marshal.AllocHGlobal(length);
                                Buffer.MemoryCopy(@netEvent.Packet.Data.ToPointer(), newPtr.ToPointer(), length,
                                    length);
                                m_ReceivedEvents.Enqueue(new FReceivedEvent()
                                    {Data = newPtr, Peer = @netEvent.Peer, EventType = EventType.Receive});
                            }
                            
                            netEvent.Packet.Dispose();
                            break;
                    }
                }
        }
    }
}