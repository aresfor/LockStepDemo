

using Lockstep.Math;
using Lockstep.Serialization;
using UnityEngine;

public class Player:Entity
{
    public int LocalId;

    public PlayerInput Input;

    public GameObject Go;

    public override void Write(Serializer writer)
    {
        base.Write(writer);
        writer.Write(LocalId);
    }

    public override void Read(Deserializer reader)
    {
        base.Read(reader);
        LocalId = reader.ReadInt32();
    }
}