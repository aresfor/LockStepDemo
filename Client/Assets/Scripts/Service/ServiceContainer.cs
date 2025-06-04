
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public class ServiceContainer:IServiceContainer, IUpdateService, IExecuteService
    {
        private List<IService> m_CachedAllServices;
        private readonly Dictionary<Type, IService> m_Services = new Dictionary<Type, IService>();
        private List<IUpdateService> m_CachedUpdateServices = new List<IUpdateService>();
        private List<IExecuteService> m_CachedExecuteServices = new List<IExecuteService>();
        public void RegisterService<T, TV>(TV service) where T : class, IService where TV : T
        {
            if (m_Services.ContainsKey(typeof(T)))
            {
                Debug.LogError($"ReRegister service: {typeof(T)}");
                return;
            }
            
            m_Services[typeof(T)] = service;
            if (service is IUpdateService updateService)
            {
                m_CachedUpdateServices.Add(updateService);
            }

            if (service is IExecuteService executeService)
            {
                m_CachedExecuteServices.Add(executeService);
            }
        }

        public List<IService> GetAllService()
        {
            if (null == m_CachedAllServices)
            {
                m_CachedAllServices = m_Services.Values.ToList();
            }
            return m_CachedAllServices;
        }

        public T GetService<T>() where T : IService
        {
            if (m_Services.ContainsKey(typeof(T)))
            {
                return (T)m_Services[typeof(T)];
            }

            return default;
        }


        //@TODO:时许
        public void RollbackTo(int currentTick, int toTick)
        {
            foreach (IService service in GetAllService())
            {
                service.RollbackTo(currentTick, toTick);
            }
        }
        //@TODO:时许
        public void Backup(int tick)
        {
            foreach (IService service in GetAllService())
            {
                service.Backup(tick);
            }
        }
        //@TODO:时许
        public void Clear(int currentTick, int targetTick)
        {
            foreach (IService service in GetAllService())
            {
                service.Clear(currentTick, targetTick);
            }
        }

        public void OnUpdate(float deltaTime)
        {
            m_CachedUpdateServices.ForEach(service => service.OnUpdate(deltaTime));
        }

        public void OnExecute(float deltaTime)
        {
            m_CachedExecuteServices.ForEach(service => service.OnExecute(deltaTime));
        }
    }
