using System;
using System.Collections.Generic;
using System.Threading;
using DisruptorUnity3d;
using ENet;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;

public class Server
{
    public int MaxPlayers = 2;
    public int MaxPeers => MaxPlayers * 2;
    private Host m_HostServer;
    private Thread m_NetThread;
    private Address m_Address;
    private readonly RingBuffer<EServerNetThreadRequest> m_Requests = new RingBuffer<EServerNetThreadRequest>(8);
    private readonly RingBuffer<EServerNetThreadResponse> m_Responses = new RingBuffer<EServerNetThreadResponse>(8);
    private readonly RingBuffer<FDisconnectData> m_DisconnectData = new RingBuffer<FDisconnectData>(128);
    private readonly RingBuffer<FReceivedEvent> m_ReceivedEvents = new RingBuffer<FReceivedEvent>(1024);
    private ServerCommandHandler m_CommandHandler;

    public EServerState State = EServerState.Stopped;
    public readonly HashSet<Peer> ConnectedPeers = new HashSet<Peer>();
    public Server()
    {
        m_CommandHandler = new ServerCommandHandler(this);
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
        
        foreach (var connectedPeer in ConnectedPeers)
        {
            m_DisconnectData.Enqueue(new FDisconnectData()
            {
                Peer =  connectedPeer
            });
        }
        EnqueueRequest(EServerNetThreadRequest.Stop);
    }

    public void ClearBuffers()
    {
        //@TODO: 清理服务器相关数据
        ConnectedPeers.Clear();
    }

    public void Execute()
    {
        while (m_Responses.TryDequeue(out var response))
            switch (response)
            {
                case EServerNetThreadResponse.StartSuccess:
                    Debug.Log("Server is working");
                    State = EServerState.Working;
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
            unsafe
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
                        var currentPeerId = (ushort)@event.Peer.ID;
                        m_CommandHandler.CurrentClientPeerID = currentPeerId;
                        Debug.LogError("Receive EventType not implement");
                        
                        break;
                    case EventType.Timeout:
                        m_CommandHandler.OnClientDisconnected(@event.Peer);
                        break;
                }
            }
    }

    
    private void NetworkThread()
    {
        while (true)
        {
            while (m_DisconnectData.TryDequeue(out var data)) data.Peer.DisconnectNow(0);

            while (m_Requests.TryDequeue(out var request))
                switch (request)
                {
                    case EServerNetThreadRequest.Start:
                        try
                        {
                            m_HostServer.Create(m_Address, MaxPeers, 2);
                            m_Responses.Enqueue(EServerNetThreadResponse.StartSuccess);
                        }
                        catch (Exception e)
                        {
                            Debug.Log(e.Message);
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
                            Debug.LogError("Receive EventType not implement");
                            Debug.Log("Packet received from - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP +
                                      ", Channel ID: " + netEvent.ChannelID + ", Data length: " +
                                      netEvent.Packet.Length);
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