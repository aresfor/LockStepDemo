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

    private HashHelper m_HashHelper;

    private bool m_bIsPursuingFrame;

    #region 配置

    public int BufferCapacity = 120;
    public int SnapShotFrameInterval = 1;

    #endregion
    
    public Client()
    {
        m_CommandHandler = new ClientCommandHandler(this);
        m_CachedFreeCallback = Marshal.GetFunctionPointerForDelegate(m_FreeCallback);
    }

    public Thread StartClient(Host host, Address address)
    {
        m_HostClient = host;
        m_HostClient.Create();
        m_bIsPursuingFrame = false;
        
        m_FrameBuffer = new FrameBuffer(BufferCapacity, SnapShotFrameInterval);
        m_HashHelper = new HashHelper(GameEntry.Instance.ServiceContainer, m_FrameBuffer);
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

    public void Update(float deltaTime)
    {
        if (false == m_GameStarted)
            return;
        
        m_TickSinceGameStart =
            (int) ((LTime.realtimeSinceStartupMS - m_GameStartTimestampMs)
                   / GameEntry.Instance.StepCountPerFrame);

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
            SendInput2Server(stepFrame);

        }
    }

    public void FillInputWithLastFrame(StepFrame stepFrame)
    {
        int tick = stepFrame.Tick;
        StepFrame lastFrame = tick == 0? null :m_FrameBuffer.GetFrame(tick - 1);
        PlayerInput localPlayerInput = stepFrame.FrameInput.Input.inputs[m_LocalPlayerId];
        for (int i = 0; i < Players.Length; ++i)
        {
            stepFrame.FrameInput.Input.inputs[i] = lastFrame != null? lastFrame.FrameInput.Input.inputs[i].Clone()
                    : new PlayerInput();
        }

        stepFrame.FrameInput.Input.inputs[m_LocalPlayerId] = localPlayerInput;
        
    }
    
    private void OnUpdateClientMode()
    {
        while (GameEntry.CurrentTick < TargetTick)
        {
            //FramePredictCount = 0;
            
            //@TODO: pool
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

            StepFrame stepFrame = new StepFrame(msgFrameInput);
            m_FrameBuffer.EnqueueLocalFrame(stepFrame);
            m_FrameBuffer.EnqueueServerFrame(stepFrame);
            
            //@TIPS: 本地可以测试回滚到任一帧，所以每帧保存快照
            Step(stepFrame, true);
        }
    }

    private void OnUpdateInternal()
    {
        int maxContinueServerTick = m_FrameBuffer.MaxContinueServerTick;
        int maxServerTickInBuffer = m_FrameBuffer.MaxServerTickInBuffer;
        var minTickToBackup = (maxContinueServerTick - (maxContinueServerTick % SnapShotFrameInterval));

        //如果超过了预测最大帧数，不step
        //目前MaxPredictFrameCount：30
        while (GameEntry.CurrentTick > m_FrameBuffer.MaxPredictFrameCount)
        {
            return;
        }

        var deadline = LTime.realtimeSinceStartupMS + m_FrameBuffer.MaxSimulationMsPerFrame;

        //追服务器帧
        while (GameEntry.CurrentTick < m_FrameBuffer.CurrentTickInServer)
        {
            StepFrame serverFrame = m_FrameBuffer.GetServerFrame(GameEntry.CurrentTick);
            if (serverFrame == null)
            {
                MarkPursuingFrame();

                return;
            }
            
            m_FrameBuffer.EnqueueServerFrame(serverFrame);
            Step(serverFrame, GameEntry.CurrentTick == minTickToBackup);
            if (LTime.realtimeSinceStartupMS > deadline && GameEntry.CurrentTick < maxServerTickInBuffer)
            {
                MarkPursuingFrame();
                return;
            }
        }

        if (m_bIsPursuingFrame)
        {
            //@TODO: 触发事件？
        }
        

        if (m_FrameBuffer.IsNeedRollback)
        {
            RollbackTo(GameEntry.CurrentTick);
            //@TODO: 验证哈希，清理哈希
            //@TODO: 重新计算下次快照保存时间
            minTickToBackup = Mathf.Min(GameEntry.CurrentTick, minTickToBackup);
            while (GameEntry.CurrentTick <= maxContinueServerTick)
            {
                StepFrame serverFrame = m_FrameBuffer.GetServerFrame(GameEntry.CurrentTick);
                //@TODO: Rollback应该清理了无效的local
                m_FrameBuffer.EnqueueLocalFrame(serverFrame);
                Step(serverFrame, minTickToBackup == GameEntry.CurrentTick);
            }

        }
        
        //预测帧，前面已经追到服务器帧了，这里直接取本地输入做预测就行
        //因为可能预测失败回滚了，还需要用服务器的他人输入来填充当前预测帧
        //@Attention: PreSend需要比predictFrameCount大吗？
        while (GameEntry.CurrentTick <= TargetTick)
        {
            StepFrame predictLocalStepFrame = m_FrameBuffer.GetLocalFrame(GameEntry.CurrentTick);
            if (null == predictLocalStepFrame)
            {
                Msg_FrameInput msgFrameInput = new Msg_FrameInput()
                {
                    Input = new FrameInput()
                    {
                        inputs = new[] { GameEntry.CurrentGameInput },
                        tick = GameEntry.CurrentTick
                    }
                };
                predictLocalStepFrame = new StepFrame(msgFrameInput);
                m_FrameBuffer.EnqueueLocalFrame(predictLocalStepFrame);
            }
            FillInputWithLastFrame(predictLocalStepFrame);
            Predict(predictLocalStepFrame);
        }
        
        //@TODO: 内部实现还没有发送哈希到服务器
        //@TIPS: 哈希不一定是每个step需要发， 服务器只需要判断某些时候的哈希不一样了告诉客户端就行
        //m_HashHelper.CheckAndSendHashCodes();
        SendHash2Server();
    }
    private void SendHash2Server()
    {
        Msg_PlayerHash playerHash = new Msg_PlayerHash();
        playerHash.Hash = GameEntry.Instance.CurrentHash;
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

    private void MarkPursuingFrame()
    {
        m_bIsPursuingFrame = true;
    }

    private void Predict(StepFrame stepFrame)
    {
        //预测的帧每帧都需要快照
        Step(stepFrame, true);

    }

    private void RollbackTo(int tick)
    {
        Debug.LogError($"do rollback to :{tick}, nextTickToCheck: {m_FrameBuffer.NextTickToCheck}");

        if (tick < 0)
        {
            Debug.LogError($"rollback to tick: {tick} < 0");
            return;
        }

        GameEntry.Instance.ServiceContainer.RollbackTo(GameEntry.CurrentTick, tick);
        
        GameEntry.CurrentTick = tick;

        int oldHash = GameEntry.Instance.CurrentHash;
        int currentHash = m_HashHelper.CalcHash();

        if (oldHash != currentHash)
        {
            Debug.LogError($"rollback to tick: {tick}, but oldHash: {oldHash} is not equal to current hash: {currentHash}");
            return;
        }


        //@Question: 貌似不需要？ 因为输入只发送和本地记录一次
        //@TODO: 清理无效的localframes
    }
    
    private void CleanUselessSnapshot(int tick)
    {
        //@TODO: implementation

    }

    private void FillPlayerInputs(StepFrame stepFrame)
    {
        PlayerInput[] playerBufferInputs = stepFrame.FrameInput.Input.inputs;
        
        for (int i = 0; i < playerBufferInputs.Length; ++i)
        {
            if (m_LocalPlayerId > playerBufferInputs.Length)
            {
                Debug.LogError($"LocalPlayerId : {m_LocalPlayerId} bigger than playerInputBufferSize: {playerBufferInputs.Length}");
                return;
            }
            PlayerInput playerBufferInput = stepFrame.FrameInput.Input.inputs[i];
            Players[i].Input = playerBufferInput.Clone();
        }
    }
    
    public void Step(StepFrame stepFrame, bool shouldSnapshot)
    {
        
        //@TODO:计算哈希
        GameEntry.Instance.CurrentHash = m_HashHelper.CalcHash();
        //@TODO: 状态备份
        GameEntry.Instance.ServiceContainer.Backup(GameEntry.CurrentTick);
        //@TODO: 状态日志
        
        StepInternal(stepFrame);
        FillPlayerInputs(stepFrame);
        m_FrameBuffer.SetNextClientTick(GameEntry.CurrentTick + 1);

        ++GameEntry.CurrentTick;

        if (shouldSnapshot) {
            //@TODO: implementation
            CleanUselessSnapshot(System.Math.Min(m_FrameBuffer.NextTickToCheck - 1, stepFrame.Tick));
            
        }
        //@TODO: Replay JumpTo and replay refracture
        // if (GameEntry.Instance.IsReplayMode)
        // {
        //     if (GameEntry.CurrentTick < Frames.Count)
        //     {
        //         Replay(deltaTime);
        //         ++GameEntry.CurrentTick;
        //         int nextClientTick = GameEntry.CurrentTick;
        //         m_FrameBuffer.SetNextClientTick(nextClientTick);
        //     }
        // }
        // else
        // {
        //     //send hash
        //     StepInternal(deltaTime);
        //
        //     SendHash2Server();
        //     ++GameEntry.CurrentTick;
        //     int nextClientTick = GameEntry.CurrentTick;
        //     m_FrameBuffer.SetNextClientTick(nextClientTick);
        // }
    }

    
    // private int GetHash()
    // {
    //     int hash = 1;
    //     int idx = 0;
    //     
    //     
    //     foreach (var entity in Players) {
    //         hash += entity.LocalId.GetHash() * PrimerLUT.GetPrimer(idx++);
    //         hash += entity.Position.GetHash() * PrimerLUT.GetPrimer(idx++);
    //     }
    //
    //     // foreach (var entity in EnemyManager.Instance.allEnemy) {
    //     //     hash += entity.currentHealth.GetHash() * PrimerLUT.GetPrimer(idx++);
    //     //     hash += entity.transform.GetHash() * PrimerLUT.GetPrimer(idx++);
    //     // }
    //
    //     return hash;
    // }


    //@TODO: implementation
    private void Replay(float deltaTime)
    {
        //StepInternal(deltaTime);
    }

    private void StepInternal(StepFrame stepFrame)
    {
        LFloat deltaTime = new LFloat(true, GameEntry.Instance.StepIntervalMS._val);

        foreach (var player in Players)
        {
            var uv = player.Input.InputUV;
            player.Position += new LVector3(uv.x,LFloat.zero, uv.y) * deltaTime;
            player.Go.transform.position = player.Position.ToVector3();
        }
        
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
        Msg_PlayerInput msgPlayerInput = new Msg_PlayerInput()
        {
            PlayerInput = GameEntry.CurrentGameInput.Clone(),
            Tick = stepFrame.Tick
        };
        
        var bytes = msgPlayerInput.ToBytes();
        var data = MessagePacker.Instance.GetBytesPtr(msgPlayerInput.OpCode, bytes, out var totalLength);
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