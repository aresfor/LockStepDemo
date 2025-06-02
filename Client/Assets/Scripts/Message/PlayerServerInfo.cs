using System;
using Lockstep.Math;
using Lockstep.Serialization;

[Serializable]
public class PlayerServerInfo : BaseFormater {
    //@TODO:
    public string name;
    public uint id;

    #region Server同步

    //@TODO:
    public int localId;
    //@TODO:
    public int PrefabId;
    public LVector3 initPos;
    public LVector3 initDeg;
    
    #endregion

    public PlayerServerInfo()
    {
    }

    public PlayerServerInfo(uint inId,  LVector3 inDeg, LVector3 inPos)
    {
        id = inId;
        initDeg = inDeg;
        initPos = inPos;
    }
    
    public override void Serialize(Serializer writer){
        writer.Write(initPos);
        writer.Write(initDeg);
        writer.Write(PrefabId);
        writer.Write(localId);
    }

    public override void Deserialize(Deserializer reader)
    {
        initPos = reader.ReadLVector3();
        initDeg = reader.ReadLVector3();
        PrefabId = reader.ReadInt32();
        localId = reader.ReadInt32();
    }

    //other infos...
}