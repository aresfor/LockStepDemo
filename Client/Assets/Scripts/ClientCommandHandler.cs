
    using ENet;
    using UnityEngine;

    public class ClientCommandHandler
    {
        private readonly Client m_Client;

        public ClientCommandHandler(Client client)
        {
            m_Client = client;
        }

        public void OnConnected(Peer serverPeer)
        {
            Debug.Log($"Connected Success, PeerID: {serverPeer.ID}, PeerPort: {serverPeer.Port}");
            m_Client.State = EClientState.Connected;
            m_Client.ServerPeer = serverPeer;

        }

        public void OnDisconnected(Peer serverPeer)
        {
            Debug.Log("Disconnected From Server");
            m_Client.CleanupState();
            m_Client.EnqueueRequest(EClientNetThreadRequest.DisconnectFromServer);
        }
    }
