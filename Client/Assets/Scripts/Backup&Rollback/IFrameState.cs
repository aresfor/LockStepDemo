
    public interface IFrameState:IRollback, IBackUp
    {
        int CurrentTick { get; }

        int Clear(int maxVerifiedTick);
    }
