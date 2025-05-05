using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using ENet;
using Lockstep.Util;
using UnityEngine;
using UnityEngine.Serialization;


public partial class GameEntry : MonoBehaviour
{
    #region Static

    public static GameEntry Instance;
    public static ENetMode NetMode;

    public static float Ping;
    public static List<float> Delays = new List<float>();
    public static PlayerInput CurGameInput;
    public static int CurTick;
    #endregion

    #region Network

    public bool IsClientMode = false;
    public bool IsReplayMode;
    public string IP = "localhost";
    public ushort Port = 9500;
    [Range(10, 60)]
    public int Step = 15;

    [Range(10, 60)]
    public int UpdateCallPerSecond = 30;
    
    public int MaxPlayers = 10;
    public int MaxPeers => MaxPlayers * 2;
    public int MaxChannels = 10;
    
    public float StepInterval => 1.0f / Step;

    private float m_LastStepTime;
    private Host m_Host;

    private Client m_Client;
    private Server m_Server;

    private Thread m_NetThread;

    #endregion
    
    public string RecordFilePath;

    
    public List<PlayerServerInfo> PlayerServerInfos = new List<PlayerServerInfo>();
    
    
    public Dictionary<int, float> Tick2SendTimer = new Dictionary<int, float>();

    public PlayerServerInfo ClientPlayerInfo;
    
    private void Awake()
    {
        Instance = this;
        
        LTime.DoStart();
        
        if (!Application.isEditor)
            IsClientMode = false;
    }

    private void Start()
    {
        m_LastStepTime = Time.time;
        
        
        if (IsReplayMode)
        {
            Debug.Log("ReplayMode Start!");

            m_Client = new Client();
            RecordHelper.Deserialize(RecordFilePath, m_Client);
            m_Client.StartGameInternal();
        }
        else if(IsClientMode)
        {
            Debug.Log("ClientMode Start!");
            m_Client = new Client();
            ClientPlayerInfo = new PlayerServerInfo();
            m_Client.StartGame(new Msg_StartGame()
            {
                playerInfos = new []{ClientPlayerInfo}
            });
        }
        
        
    }

    public void StartHost()
    {
        m_Host = new Host();
        Address address = new Address();

        address.SetHost(IP);
        address.Port = Port;

        if (NetMode == ENetMode.Client)
        {
            m_Client = new Client();
            m_NetThread = m_Client.StartClient(m_Host, address);
        }
        else if (NetMode == ENetMode.Server)
        {
            m_Server = new Server();
            m_NetThread = m_Server.StartServer(IP, Port);
        }
        else
        {
            Debug.LogError($"NetMode Missing Case: {NetMode}");
        }
    }

    public static void OnHostFinish()
    {
        GameEntry.NetMode = ENetMode.None;
        ClearScreenLog();
    }
    private void Update()
    {
        if (NetMode is ENetMode.Client)
        {
            m_Client?.UpdateNetwork();
        }
        m_Client?.OnUpdate(Time.deltaTime);
        
        while (m_LastStepTime + StepInterval < Time.time)
        {
            //Execute(Time.time - m_LastStepTime);
            Execute(StepInterval);
            m_LastStepTime += StepInterval;
        }

    }

    private void Execute(float deltaTime)
    {
        
        //@TIPS: client可能因为网络原因改变本地stepInterval
        //，因此在其OnUpdate中进行Execute的调用，这里只对server execute
        //，具体见Client.OnUpdate
        if(IsClientMode || IsReplayMode)
            m_Client?.Execute(deltaTime);
        
        if(NetMode is ENetMode.Client)
            m_Client?.Execute(deltaTime);

        if (NetMode is ENetMode.Server)
            m_Server?.Execute(deltaTime);
    }
    

    private void OnDestroy()
    {
        if (m_Client != null)
        {
            if (!IsReplayMode)
            {
                RecordHelper.Serialize(RecordFilePath, m_Client);
            }
            m_Client.StopClient();
            m_Client = null;
            
        }
        else if (m_Server != null)
        {
            m_Server.StopServer();
            m_Server = null;
        }
    }
    
}