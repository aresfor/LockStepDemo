using System;
using Lockstep.Serialization;


[Serializable]
public class FrameInput : BaseFormater {
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
}