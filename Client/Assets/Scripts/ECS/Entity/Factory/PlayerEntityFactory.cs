

    using Entitas;

    public class PlayerEntityFactory:IEntityFactory
    {
        public EEntityArchetype Type { get; } = EEntityArchetype.Player;

        public Entity CreateEntity(Context context, EntitySpawnParameter spawnParameter)
        {
            throw new System.NotImplementedException();
        }

        public Entity GetEntity(Context context, int entityId)
        {
            throw new System.NotImplementedException();
        }

        public void DestroyEntity(int entityId)
        {
            //@TODO: 
            throw new System.NotImplementedException();

            Entity entity;
            entity.Destroy();
        }
    }
