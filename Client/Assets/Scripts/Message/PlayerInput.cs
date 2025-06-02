
using System;
using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Serialization;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class PlayerInput:BaseFormater, IEquatable<PlayerInput>
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

    public override string ToString()
    {
        return $"MousePos: {MousePos}, InputUV: {InputUV}, IsSprint: {IsSprint}， IsFire: {IsFire}";
    }

    public bool Equals(PlayerInput other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return MousePos.Equals(other.MousePos) && InputUV.Equals(other.InputUV) && IsSprint == other.IsSprint && IsFire == other.IsFire;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((PlayerInput)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(MousePos, InputUV, IsSprint, IsFire);
    }
}