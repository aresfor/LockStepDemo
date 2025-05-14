
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public class ServiceContainer:IServiceContainer
    {
        private Dictionary<Type, IService> m_Services = new Dictionary<Type, IService>();

        private bool m_Dirty = false;
        private List<IService> m_CachedAllServices;
        public void RegisterService<T, TV>(TV service) where T : class, IService where TV : T
        {
            if (m_Services.ContainsKey(typeof(T)))
            {
                Debug.LogError($"ReRegister service: {typeof(T)}");
                return;
            }

            m_Dirty = true;
            m_Services[typeof(T)] = service;

        }

        public List<IService> GetAllService()
        {
            if (m_Dirty || null == m_CachedAllServices)
            {
                m_CachedAllServices = m_Services.Values.ToList();
                m_Dirty = false;
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
    }
