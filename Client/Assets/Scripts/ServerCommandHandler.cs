
    using System;
    using System.Runtime.InteropServices;
    using ENet;
    using Lockstep.Logic;
    using Lockstep.Math;
    using UnityEngine;

    public class ServerCommandHandler
    {
        private Server m_Server;
        private int m_ConnectedPlayerCount = 0;
        
        //public int CurrentClientPeerID;
        public ServerCommandHandler(Server server)
        {
            m_Server = server;
        }
        
        public void OnClientConnected(Peer peer)
        {
            Debug.Log($"Client connected - {peer.ID}");

            if (m_ConnectedPlayerCount == GameEntry.Instance.MaxPlayers)
            {
                m_Server.EnqueueDisconnectData(new FDisconnectData {Peer = peer});
                return;
            }
            
            ++m_ConnectedPlayerCount;
            Debug.Log($"CurrentPlayerCount: {m_ConnectedPlayerCount}");
            m_Server.Id2ConnectedPeers.Add(peer.ID, peer);

            m_Server.Id2PlayerInfos.Add(peer.ID, new PlayerServerInfo(peer.ID
                , new LVector3()
                , new LVector3()));
            
            //@TODO: 
            //m_Server.Name2PlayerInfos
            
            
        }

        public void OnClientDisconnected(Peer peer)
        {
            Debug.Log($"Client disconnected - {peer.ID}");
            --m_ConnectedPlayerCount;
            Debug.Log($"CurrentPlayerCount: {m_ConnectedPlayerCount}");
            m_Server.Id2ConnectedPeers.Remove(peer.ID);

            m_Server.Id2PlayerInfos.Remove(peer.ID);
            // var e = _game.GetEntityWithConnection((ushort) peer.ID);
            //
            // if (e != null) e.isDestroyed = true;
        }


        public void OnReceive(ref FReceivedEvent @event)
        {
            IntPtr data = @event.Data;
            int offset = 0;
            ushort opCode = (ushort)Marshal.ReadInt16(data);
            offset += 2;
            ushort size = (ushort)Marshal.ReadInt16(data, offset);
            offset += 2;

            Debug.Log($"server Receive:: opCode: {(EMessageType)opCode}, size: {size}");

            //@TODO: 建立缓存区
            byte[] inputData = new byte[size];
            IntPtr offsetData = IntPtr.Add(data, offset);

            Marshal.Copy(offsetData, inputData, 0, size);

            var message = MessagePacker.Instance.DeserializeFrom(opCode, inputData
                , 0, size) as IMessage;

            EMessageType msgType = (EMessageType)opCode;
            switch (msgType)
            {
                case EMessageType.PlayerInput:
                    m_Server.OnPlayerInput(@event.Peer, message);
                    break;
                case EMessageType.StartGame:
                    m_Server.OnStartGame(message);
                    break;
                case EMessageType.PlayerHash:
                    m_Server.OnPlayerHash(@event.Peer, message);
                    break;
                default:
                    Debug.LogError($"MissingCase msgType: {msgType}");
                    break;
            }
            
            //释放native内存
            Marshal.FreeHGlobal(data);
        }
    }
