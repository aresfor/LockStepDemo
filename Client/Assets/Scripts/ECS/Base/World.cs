using System.Collections.Generic;
using Entitas;
using Entitas.VisualDebugging.Unity;

public class World
{
    public Contexts Contexts { get; private set; }
    public Systems Systems { get; private set; }
    public ServiceContainer ServiceContainer { get; private set; }
    public ENetMode NetMode { get; private set; }
    
    public string Name { get; private set; }

    private EntityManager m_EntityManager;
    
    public World(string name)
    {
        Contexts = new Contexts();
        Name = name;
    }

    public void Initialize(Systems inSystems, ServiceContainer inServiceContainer, ENetMode inNetMode)
    {
        NetMode = inNetMode;
        Systems = inSystems;
        ServiceContainer = inServiceContainer;
        m_EntityManager = new EntityManager(this, Contexts);
        Systems.Initialize();
    }

    public List<IService> GetAllService() => ServiceContainer.GetAllService();

    public T GetService<T>() where T : IService => ServiceContainer.GetService<T>();

    public bool IsListenServer => NetMode == ENetMode.Server;
    public bool IsClient => NetMode == ENetMode.Client;

    public T GetUniqueComponent<T>() where T : IComponent, IUnique => Contexts.defaultContext.GetUnique<T>();
    public Entity GetSingletonEntity<T>() where T : IComponent, IUnique => Contexts.defaultContext.GetSingleEntity<T>();

    public Entity CreateEntity<C>(EEntityArchetype archetype, EntitySpawnParameter spawnParameter) where C : ContextAttribute =>
        m_EntityManager.CreateEntity<C>(archetype, spawnParameter);

    public Entity CreateGameEntity(EEntityArchetype archetype, EntitySpawnParameter spawnParameter) =>
        CreateEntity<Game>(archetype, spawnParameter);

    public void DestroyEntity(EEntityArchetype archetype, int entityId) =>
        m_EntityManager.DestroyEntity(archetype, entityId);

    public Entity GetPlayerEntity(int entityId) => m_EntityManager.GetEntity<Game>(EEntityArchetype.Player, entityId);
    
    public void Update(float deltaTime)
    {
        Systems.Update(deltaTime);
    }

    public void Execute()
    {
        Systems.Execute();
        Systems.Cleanup();
    }
}