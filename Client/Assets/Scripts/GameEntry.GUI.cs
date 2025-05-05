
    using UnityEngine;

    public partial class GameEntry
    {
        
    private void OnGUI()
    {
        if (IsClientMode || IsReplayMode)
            return;
        
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

                        if (GUILayout.Button("StartGame"))
                        {
                            m_Client.SendStartGame2Server();
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
                        if (GUILayout.Button("StartGame"))
                        {
                            m_Server.ServerStartGame();
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

    }
