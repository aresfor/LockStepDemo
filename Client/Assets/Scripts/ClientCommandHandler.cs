using System;
using System.Runtime.InteropServices;
using ENet;
using Lockstep.Logic;
using Lockstep.Serialization;
using Lockstep.Util;
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
        m_Client.ConnectedServerPeer = serverPeer;
    }

    public void OnDisconnected(Peer serverPeer)
    {
        Debug.Log("Disconnected From Server");
        m_Client.CleanupState();
        m_Client.EnqueueRequest(EClientNetThreadRequest.DisconnectFromServer);
    }

    public void OnReceive(IntPtr data)
    {
        int offset = 0;
        ushort opCode = (ushort)Marshal.ReadInt16(data);
        offset += 2;
        ushort size = (ushort)Marshal.ReadInt16(data, offset);
        offset += 2;
        
        Debug.Log($"client Receive:: opCode: {(EMessageType)opCode}, size: {size}");

        //@TODO: 建立缓存区
        byte[] inputData = new byte[size];
        IntPtr offsetData = IntPtr.Add(data, offset);
        Marshal.Copy(offsetData, inputData, 0, size);
        
        var message = MessagePacker.Instance.DeserializeFrom(opCode, inputData
            , 0, size) as IMessage;

        EMessageType msgType = (EMessageType)opCode;
        switch (msgType)
        {
            case EMessageType.FrameInput:
                m_Client.OnFrameInput(message);
                break;
            case EMessageType.StartGame:
                m_Client.StartGame(message);
                break;
            default:
                Debug.LogError($"MissingCase msgType: {msgType}");
                break;
        }
        
        //释放native内存
        Marshal.FreeHGlobal(data);
    }
}