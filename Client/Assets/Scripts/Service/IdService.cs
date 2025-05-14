
    using System.Collections.Generic;
    using UnityEngine;

    public class IdService:BaseService, IIdService
    {
        private Dictionary<int, int> m_Tick2Id = new Dictionary<int, int>();

        private int m_Id;

        public int GenerateId()
        {
            return ++m_Id;
        }
        
        public override void RollbackTo(int currentTick, int toTick)
        {
            if (false == m_Tick2Id.ContainsKey(toTick))
            {
                Debug.LogError($"{nameof(IdService)} has no tick2Id, tick: {toTick}");
                return;
            }

            m_Id = m_Tick2Id[toTick];
        }

        public override void Backup(int tick)
        {
            if (m_Tick2Id.ContainsKey(tick))
            {
                Debug.LogError($"{nameof(IdService)} has already contains tick backup, tick: {tick}, tickId: {m_Tick2Id[tick]}");
            }

            m_Tick2Id[tick] = m_Id;
        }

        public override void Clear(int currentTick, int targetTick)
        {
            for (int i = currentTick; i <= targetTick; ++i)
            {
                if (m_Tick2Id.ContainsKey(i))
                {
                    m_Tick2Id.Remove(i);
                }
            }
        }
    }
