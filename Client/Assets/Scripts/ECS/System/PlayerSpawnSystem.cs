using Entitas;

[Game]
public class PlayerSpawnSystem : SystemBase, IExecuteSystem
{
    private PlayerLifetimeComponent m_PlayerLifetimeComponent;

    public PlayerSpawnSystem(World world) : base(world)
    {
        m_PlayerLifetimeComponent = world.GetUniqueComponent<PlayerLifetimeComponent>();
    }

    //@TODO:每帧生成限制
    public void Execute()
    {
        while (m_PlayerLifetimeComponent.WaitingSpawnPlayers.Count > 0)
        {
            FPlayerSpawnInfo spawnInfo = m_PlayerLifetimeComponent.WaitingSpawnPlayers.Dequeue();
            //@TODO:池化参数
            PlayerSpawnParameter spawnParameter = new PlayerSpawnParameter()
            {
                SpawnInfo = spawnInfo
            };

            World.CreateGameEntity(EEntityArchetype.Player, spawnParameter);
        }

        while (m_PlayerLifetimeComponent.WaitingDespawnPlayers.Count > 0)
        {
            int despawnPlayerEntityId = m_PlayerLifetimeComponent.WaitingDespawnPlayers.Dequeue();

            World.DestroyEntity(EEntityArchetype.Player, despawnPlayerEntityId);
        }
    }
}