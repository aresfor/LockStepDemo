

using Lockstep.Math;
using Lockstep.Serialization;
using UnityEngine;

public class Player:Entity
{
    public int LocalId;

    public PlayerInput Input;

    public GameObject Go;

    public int PrefabId;

    public override void Write(Serializer writer)
    {
        base.Write(writer);
        writer.Write(LocalId);
        writer.Write(PrefabId);
    }

    public override void Read(Deserializer reader)
    {
        base.Read(reader);
        LocalId = reader.ReadInt32();
        PrefabId = reader.ReadInt32();
        
    }

    public override void AfterSerialize()
    {
        base.AfterSerialize();
        
        Go.transform.position = Position.ToVector3();
        Go.transform.rotation = Quaternion.Euler(Euler.ToVector3());
    }

    public override void DoBindRef()
    {
        base.DoBindRef();
        //@TODO: viewComponent
        Go = GameObject.Find("Player_" + LocalId);
        Debug.LogError($"Find ref: {Go}");
    }
}