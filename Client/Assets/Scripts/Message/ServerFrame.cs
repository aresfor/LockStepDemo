using System;

[Serializable]
public class StepFrame:IEquatable<StepFrame>
{
    public Msg_FrameInput FrameInput;

    public int Tick => FrameInput.Input.tick;

    public StepFrame(Msg_FrameInput inFrameInput)
    {
        FrameInput = inFrameInput;
    }

    public override string ToString()
    {
        return FrameInput != null ? FrameInput.ToString() : string.Empty;
    }

    public bool Equals(StepFrame other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(FrameInput, other.FrameInput);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((StepFrame)obj);
    }

    public override int GetHashCode()
    {
        return (FrameInput != null ? FrameInput.GetHashCode() : 0);
    }
}