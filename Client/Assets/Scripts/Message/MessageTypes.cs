using System.Collections.Generic;
using Lockstep.Serialization;

public enum EMessageType : ushort
{
    None = 0,
    //客户端上传
    PlayerInput = 1,
    //服务器下发
    FrameInput = 2,
    PlayerHash = 3,
    StartGame = 4,
    EndGame = 5,

    //@TODO:
    JoinRoom = 6,
    QuitRoom = 7
}

public interface IMessage
{
    public ushort OpCode { get; set; }
    public ushort Size { get; set; }
}


public class Msg_PlayerInput:BaseFormater, IMessage
{
    public ushort OpCode { get; set; } = (ushort)EMessageType.PlayerInput;
    public ushort Size { get; set; }
    public PlayerInput PlayerInput;
    public int Tick;

    public override void Serialize(Serializer writer)
    {
        writer.Write(Tick);
        writer.Write(PlayerInput);
    }

    public override void Deserialize(Deserializer reader)
    {
        Tick = reader.ReadInt32();
        PlayerInput = reader.ReadRef(ref PlayerInput);
    }
}

public class Msg_FrameInput :BaseFormater,  IMessage
{
    public ushort OpCode { get; set; } = (ushort)EMessageType.FrameInput;
    public ushort Size { get; set; }
    public FrameInput Input;

    public override void Serialize(Serializer writer)
    {

        writer.Write(Input);
    }

    public override void Deserialize(Deserializer reader)
    {
        Input = reader.ReadRef(ref Input);
    }
}


public class Msg_PlayerHash : BaseFormater, IMessage
{
    public ushort OpCode { get; set; } = (ushort)EMessageType.PlayerHash;
    public ushort Size { get; set; }
    public int Tick;
    public int Hash;

    public override void Serialize(Serializer writer)
    {

        writer.Write(Tick);
        writer.Write(Hash);
    }

    public override void Deserialize(Deserializer reader)
    {

        Tick = reader.ReadInt32();
        Hash = reader.ReadInt32();
    }
}

public class Msg_StartGame :BaseFormater,  IMessage
{
    public ushort OpCode { get; set; } = (ushort)EMessageType.StartGame;
    public ushort Size { get; set; }

    public int mapId;
    public int localPlayerId;
    public PlayerServerInfo[] playerInfos;

    public override void Serialize(Serializer writer){

        writer.Write(mapId);
        writer.Write(localPlayerId);
        writer.Write(playerInfos);
    }

    public override void Deserialize(Deserializer reader){
        
        mapId = reader.ReadInt32();
        localPlayerId = reader.ReadInt32();
        playerInfos = reader.ReadArray(playerInfos);
    }
}

public class Msg_EndGame : BaseFormater, IMessage
{
    public ushort OpCode { get; set; } = (ushort)EMessageType.EndGame;
    public ushort Size { get; set; }
}
