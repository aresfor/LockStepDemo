
    public interface IFrameState:IRollback, IBackUp
    {
        //int CurrentTick { get; }

        void Clear(int currentTick, int targetTick);
    }
