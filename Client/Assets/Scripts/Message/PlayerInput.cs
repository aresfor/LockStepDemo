
using System;
using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Serialization;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class PlayerInput:BaseFormater
{
    public LVector2 MousePos;
    public LVector2 InputUV;
    public bool IsSprint;
    public bool IsFire;

    public override void Serialize(Serializer writer){
        writer.Write(MousePos);
        writer.Write(InputUV);
        writer.Write(IsSprint);
        writer.Write(IsFire);
    }

    public override void Deserialize(Deserializer reader){
        MousePos = reader.ReadLVector2();
        InputUV = reader.ReadLVector2();
        IsSprint = reader.ReadBoolean();
        IsFire = reader.ReadBoolean();
    }

    public PlayerInput Clone(){
        return new PlayerInput() {
            MousePos = this.MousePos,
            InputUV = this.InputUV,
            IsSprint = this.IsSprint,
            IsFire = this.IsFire,
        };
    }
}