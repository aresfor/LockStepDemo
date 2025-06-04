
    public class PlayerSpawnParameter:EntitySpawnParameter
    {
        public FPlayerSpawnInfo SpawnInfo;


        public override void Clear()
        {
            SpawnInfo = default;
        }
    }
