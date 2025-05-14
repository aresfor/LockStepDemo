
    using System.Collections.Generic;

    public class GameSnapShotContainer
    {
        public FGameSnapShot[] SnapShots;

        public int CurrentIndex;

        public int Capacity => SnapShots?.Length ?? 0;
        public GameSnapShotContainer(int capacity)
        {
            SnapShots = new FGameSnapShot[capacity];
        }

        public void Enqueue(FGameSnapShot snapShot)
        {
            SnapShots[CurrentIndex % Capacity] = snapShot;
        }
    }
    public struct FGameSnapShot
    {
        public int Tick;
        public byte[] SnapShotBytes;
        
        public FGameSnapShot(int inTick, byte[] inSnapShotBytes)
        {
            Tick = inTick;
            SnapShotBytes = inSnapShotBytes;
        }
    }
