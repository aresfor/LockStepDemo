using System;
using Lockstep.Serialization;


[Serializable]
public class FrameInput : BaseFormater, IEquatable<FrameInput>
{
    public int tick;
    public PlayerInput[] inputs;

    public override void Serialize(Serializer writer){
        writer.Write(tick);
        writer.Write(inputs);
    }

    public override void Deserialize(Deserializer reader){
        tick = reader.ReadInt32();
        inputs = reader.ReadArray(inputs);
    }

    public FrameInput Clone(){
        var tThis = this;
        var val = new FrameInput() {tick = tThis.tick};
        if (tThis.inputs == null) return val;
        val.inputs = new PlayerInput[tThis.inputs.Length];
        for (int i = 0; i < val.inputs.Length; i++) {
            val.inputs[i] = tThis.inputs[i].Clone();
        }

        return val;
    }

    public override string ToString()
    {
        string s = string.Empty;
        if (inputs == null)
            return string.Empty;

        int i = -1;
        s += $"tick: {tick}\n";
        foreach (var playerInput in inputs)
        {
            ++i;
            if (playerInput == null)
            {
                s += $"playerLocalId: {i}, input: null\n";
            }
            else
            {
                s += $"playerLocalId: {i}, input: {playerInput.ToString()}\n";
            }
        }

        return s;
    }

    public bool Equals(FrameInput other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        bool inputEquatable = true;
        if (inputs == null && other.inputs == null)
        {
            inputEquatable = true;
        }
        else if (other.inputs != null && inputs != null && other.inputs.Length == inputs.Length)
        {
            for (int i = 0; i < inputs.Length; ++i)
            {
                if (other.inputs[i] == null && inputs[i] == null)
                {
                    inputEquatable = true;
                }
                else if (other.inputs[i] != null
                         && inputs[i] != null )
                {
                    inputEquatable = other.inputs[i].Equals(inputs[i]);
                    if (false == inputEquatable)
                    {
                        break;
                    }
                }
                else
                {
                    inputEquatable = false;
                    break;
                }
            }
        }
        else
        {
            inputEquatable = false;
        }
        
        return tick == other.tick && inputEquatable;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((FrameInput)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(tick, inputs);
    }
}