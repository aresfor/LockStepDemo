
    public interface ITimeMachine
    {
        int CurrentTick { get; }
        //Rollback to tick , so all cmd between [tick,~)(Include tick) should undo
        void Rollback(int tick);
        void Backup(int tick);
        //Discard all cmd between [0,maxVerifiedTick] (Include maxVerifiedTick)
        void Clean(int maxVerifiedTick);

    }
