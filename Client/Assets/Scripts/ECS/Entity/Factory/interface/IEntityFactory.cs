
    using Entitas;

    public interface IEntityFactory
    {
        public EEntityArchetype Type { get; }
        public Entity CreateEntity(Context context, EntitySpawnParameter spawnParameter);
        public Entity GetEntity(Context context, int entityId);

        public void DestroyEntity(int entityId);
    }
