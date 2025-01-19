using System;
using System.Collections;
using System.Threading;
using ENet;
using UnityEngine;


public class GameEntry : MonoBehaviour
{
    public static ENetMode NetMode;
    public string IP = "localhost";
    public ushort Port = 9500;
    [Range(10, 60)]
    public int Step = 15;
    public float StepInterval => 1.0f / Step;

    private float m_LastStepTime;
    private Host m_Host;
    private Peer m_Peer;
    private Client m_Client;
    private Server m_Server;

    private Thread m_NetThread;
    private void Start()
    {
        m_LastStepTime = Time.time;
    }

    public void StartHost()
    {
        m_Host = new Host();
        m_Host.Create();
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
    }
    private void Update()
    {
        if(NetMode is ENetMode.Client)
            m_Client?.UpdateNetwork();
        
        while (m_LastStepTime + StepInterval < Time.time)
        {
            Execute();
            m_LastStepTime += StepInterval;
        }

    }

    private void Execute()
    {
        if(NetMode is ENetMode.Client)
            m_Client?.Execute();

        if (NetMode is ENetMode.Server)
            m_Server?.Execute();
    }

    private void OnGUI()
    {
        if (NetMode != ENetMode.None)
        {
            GUILayout.BeginVertical();
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Running On: {NetMode}");
                GUILayout.EndHorizontal();
            }
            
            {
                GUILayout.BeginHorizontal();

                if (NetMode is ENetMode.Client)
                {
                    if (m_Client.State is EClientState.Connected)
                    {
                        if (GUILayout.Button("StopClient"))
                        {
                            m_Client.StopClient();
                        }
                    }
                    else if (m_Client.State is EClientState.Connecting)
                    {
                        GUILayout.Label("Connecting......");
                        if (GUILayout.Button("CancelConnect2Server"))
                        {
                            m_Client.CancelConnect2Server();
                        }
                    }
                    else
                    {
                        GUILayout.Label($"isn't connecting: {m_Client.State}");
                        if (GUILayout.Button("CancelConnect2Server"))
                        {
                            m_Client.CancelConnect2Server();
                        }
                    }
                    
                }
                else if (NetMode is ENetMode.Server)
                {
                    if (m_Server.State is EServerState.Working)
                    {
                        if (GUILayout.Button("StopServer"))
                        {
                            m_Server.StopServer();
                        }
                    }
                    else if (m_Server.State is EServerState.Starting)
                    {
                        GUILayout.Label("Starting......");
                        if (GUILayout.Button("CancelStartServer"))
                        {
                            m_Server.CancelStartServer();
                        }
                    }
                    else
                    {
                        GUILayout.Label($"isn't staring: {m_Server.State}");
                        if (GUILayout.Button("CancelStartServer"))
                        {
                            m_Server.CancelStartServer();
                        }
                    }
                    
                }
                GUILayout.EndHorizontal();
            }
            
            GUILayout.EndVertical();
        }
        else
        {
            GUILayout.BeginVertical();
        
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Choose Start Server/Client");
                GUILayout.EndHorizontal();
            }

            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Start Server"))
                {
                    NetMode = ENetMode.Server;
                    StartHost();
                }

                if (GUILayout.Button("Start Client"))
                {
                    NetMode = ENetMode.Client;
                    StartHost();
                }
            
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }
    }

    private void OnDestroy()
    {
        if (m_Client != null)
        {
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