
    using System.Collections.Generic;
    using Lockstep.Math;
    using UnityEngine;

    public class IdService:BaseService, IIdService
    {
        private Dictionary<int, int> m_Tick2Id = new Dictionary<int, int>();

        private int m_Id;

        public int GenerateId()
        {
            return ++m_Id;
        }
        public override int GetHash(ref int idx)
        {
            return m_Id * PrimerLUT.GetPrimer(idx++);
        }
        
        public override void RollbackTo(int currentTick, int toTick)
        {
            m_Id = m_Tick2Id[toTick];
        }

        public override void Backup(int tick)
        {
            Debug.LogError($"Backup tick: {tick}");
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
