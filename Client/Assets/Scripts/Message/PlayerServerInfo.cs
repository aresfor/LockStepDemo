using System;
using Lockstep.Math;
using Lockstep.Serialization;

[Serializable]
public class PlayerServerInfo : BaseFormater {
    //@TODO:
    public string name;
    public uint id;
    //@TODO:
    public int localId;
    public LVector3 initPos;
    public LFloat initDeg;
    //@TODO:
    public int PrefabId;

    public PlayerServerInfo()
    {
    }

    public PlayerServerInfo(uint inId, LFloat inDeg, LVector3 inPos)
    {
        id = inId;
        initDeg = inDeg;
        initPos = inPos;
    }
    
    public override void Serialize(Serializer writer){
        writer.Write(initPos);
        writer.Write(initDeg);
        writer.Write(PrefabId);
    }

    public override void Deserialize(Deserializer reader)
    {
        initPos = reader.ReadLVector3();
        initDeg = reader.ReadLFloat();
        PrefabId = reader.ReadInt32();
    }

    //other infos...
}