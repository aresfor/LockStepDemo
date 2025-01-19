using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using DisruptorUnity3d;
using ENet;
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

    private readonly ClientCommandHandler m_CommandHandler;
    public Peer ServerPeer;
    public EClientState State = EClientState.Disconnected;

    public Client()
    {
        m_CommandHandler = new ClientCommandHandler(this);
    }

    public Thread StartClient(Host host, Address address)
    {
        m_HostClient = host;
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
        ServerPeer = new Peer();
        //ConnectionId.IsSet = false;
        //_firstPacket       = true;

        //_syncGroup.GetEntities(_syncBuffer);
        //foreach (var e in _syncBuffer) e.isDestroyed = true;

        //EnqueuedCommandCount = 0;
        //ToServer.Clear();
        //_fromServer.Clear();
    }

    public void Execute()
    {
        //@TODO:    
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
                    Debug.LogError("Not ImplementReceive");
                    //_states.Enqueue(@event.Data);
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
            while (m_Requests.TryDequeue(out var request))
                switch (request)
                {
                    case EClientNetThreadRequest.Connect:
                        try
                        {
                            m_HostClient.Connect(m_Address, 2);
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
                        ServerPeer.DisconnectNow(0);
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
                            Debug.LogError("Not Implement Receive");
                            Debug.Log("Packet received from server - Channel ID: " + netEvent.ChannelID +
                                      ", Data length: " + netEvent.Packet.Length);
                            netEvent.Packet.Dispose();
                            break;
                    }
                }
        }
    }
}