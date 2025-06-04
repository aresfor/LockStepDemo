
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Entitas;

    public class EntityManager
    {
        
        public Dictionary<EEntityArchetype, IEntityFactory> m_Archetype2EntityFactory =
            new Dictionary<EEntityArchetype, IEntityFactory>();

        private Contexts m_Contexts;
        private World m_World;

        public EntityManager(World world, Contexts inContexts)
        {
            //@TODO: factory initialize

            m_World = world;
            m_Contexts = inContexts;

            InitializeFactory();
        }

        private void InitializeFactory()
        {
            var factoryType = typeof(IEntityFactory);
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => p.IsClass
                            && p.IsPublic
                            && !p.IsAbstract
                            && factoryType.IsAssignableFrom(p));
            
            foreach (var type in types)
            {
                var factoryInstance = (IEntityFactory)Activator.CreateInstance(type);
                m_Archetype2EntityFactory.Add(factoryInstance.Type, factoryInstance);
            }
            
        }
       
        
        public Entity CreateEntity<C>(EEntityArchetype entityArchetype, EntitySpawnParameter spawnParameter) where C:ContextAttribute
        {
            return m_Archetype2EntityFactory[entityArchetype].CreateEntity(m_Contexts.GetContext<C>(), spawnParameter);
        }

        public Entity GetEntity<C>(EEntityArchetype entityArchetype, int entityId) where C:ContextAttribute
        {
            return m_Archetype2EntityFactory[entityArchetype].GetEntity(m_Contexts.GetContext<C>(), entityId);
        }

        public void DestroyEntity(EEntityArchetype entityArchetype, int entityId)
        {
            m_Archetype2EntityFactory[entityArchetype].DestroyEntity(entityId);
        }
    }
