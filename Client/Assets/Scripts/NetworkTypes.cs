

using System;
using ENet;



#region Client
public enum EClientState
{
    Disconnected,
    Connecting,
    WaitingForId,
    Connected,
    Disconnecting
}


public enum EClientNetThreadResponse
{
    ConnectCancelled,
    ConnectFailure,
    Disconnected
}

public enum EClientNetThreadRequest
{
    Connect,
    CancelConnect,
    Disconnect,
    DisconnectFromServer
}

#endregion


#region Server
public enum EServerNetThreadRequest
{
    Start,
    Stop
}
public enum EServerNetThreadResponse
{
    StartSuccess,
    StartFailure,
    Stoppoed
}

public enum EServerState
{
    Stopped,
    Starting,
    Working,
    Stopping
}

public struct FDisconnectData
{
    public Peer Peer;
}

#endregion

#region Common

public enum ENetMode
{
    None,
    Server,
    Client
}

public struct FReceivedEvent
{
    public EventType EventType;
    public Peer      Peer;
    public IntPtr    Data;
}

public struct FSendData
{
    public Peer   Peer;
    public IntPtr Data;
    public int    Length;
}
#endregion



