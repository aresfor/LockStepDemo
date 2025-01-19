
    using ENet;
    using UnityEngine;

    public class ServerCommandHandler
    {
        private Server m_Server;
        private int m_ConnectedPlayerCount = 0;
        public int CurrentClientPeerID;
        public ServerCommandHandler(Server server)
        {
            m_Server = server;
        }
        
        public void OnClientConnected(Peer peer)
        {
            Debug.Log($"Client connected - {peer.ID}");

            if (m_ConnectedPlayerCount == m_Server.MaxPlayers)
            {
                m_Server.EnqueueDisconnectData(new FDisconnectData {Peer = peer});
                return;
            }

            //@TODO: 维护客户端代理
            ++m_ConnectedPlayerCount;
            Debug.Log($"CurrentPlayerCount: {m_ConnectedPlayerCount}");
            m_Server.ConnectedPeers.Add(peer);
            // var e  = _game.CreateEntity();
            // e.isSync = true;
            // e.AddConnectionPeer(peer);
            // e.AddConnection(id);
            // e.AddClientDataBuffer(0, new BitBuffer(64));
            // e.isRequiresWorldState = true;
            //
            // _server.EnqueueCommandForClient(id, new ServerGrantedIdCommand {Id         = id});
            // _server.EnqueueCommandForClient(id, new ServerSetTickrateCommand {Tickrate = _server.TickRate});
        }

        public void OnClientDisconnected(Peer peer)
        {
            Debug.Log($"Client disconnected - {peer.ID}");
            --m_ConnectedPlayerCount;
            Debug.Log($"CurrentPlayerCount: {m_ConnectedPlayerCount}");
            m_Server.ConnectedPeers.Remove(peer);

            // var e = _game.GetEntityWithConnection((ushort) peer.ID);
            //
            // if (e != null) e.isDestroyed = true;
        }
    }
