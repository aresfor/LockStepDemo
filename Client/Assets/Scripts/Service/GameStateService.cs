
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Lockstep.Math;
    using Lockstep.Serialization;
    using UnityEngine;

    public class GameStateService:BaseService, IGameStateService
    {
        
        private FGameState m_GameState;

        private Dictionary<Type, IList> m_Type2Entities = new Dictionary<Type, IList>();
        
        private Dictionary<int, Entity> m_Id2Entities = new Dictionary<int, Entity>();
                
        private Dictionary<int, Serializer> m_Tick2Backup = new Dictionary<int, Serializer>();


        #region Entity接口
        
        private void AddEntity<T>(T e) where T : Entity{
            if (typeof(T) == typeof(Player)) {
                int i = 0;
                Debug.Log("Add Player");
            }

            var t = e.GetType();
            if (m_Type2Entities.TryGetValue(t, out var lstObj)) {
                var lst = lstObj as List<T>;
                lst.Add(e);
            }
            else {
                var lst = new List<T>();
                m_Type2Entities.Add(t, lst);
                lst.Add(e);
            }

            m_Id2Entities[e.EntityId] = e;
        }

        private void RemoveEntity<T>(T e) where T : Entity{
            var t = e.GetType();
            if (m_Type2Entities.TryGetValue(t, out var lstObj)) {
                lstObj.Remove(e);
                m_Id2Entities.Remove(e.EntityId);
            }
            else {
                Debug.LogError("Try remove a deleted Entity" + e);
            }
        }

        private List<T> GetEntities<T>(){
            var t = typeof(T);
            if (m_Type2Entities.TryGetValue(t, out var lstObj)) {
                return lstObj as List<T>;
            }
            else {
                var lst = new List<T>();
                m_Type2Entities.Add(t, lst);
                return lst;
            }
        }
        
        public object GetEntity(int id){
            if (m_Id2Entities.TryGetValue(id, out var val)) {
                return val;
            }

            return null;
        }

        public Player[] GetPlayers(){
            return GetEntities<Player>().ToArray();
        }
        
        public T CreateEntity<T>(int prefabId, LVector3 position, LVector3 euler) where T : Entity, new(){
            Debug.Log($"CreateEntity {prefabId} pos {prefabId}");
            var baseEntity = new T();
            //@TODO: entity config
            //_gameConfigService.GetEntityConfig(prefabId)?.CopyTo(baseEntity);
            baseEntity.EntityId = GameEntry.Instance.ServiceContainer.GetService<IIdService>().GenerateId();
            // baseEntity.PrefabId = prefabId;
            // baseEntity.GameStateService = _gameStateService;
            // baseEntity.ServiceContainer = _serviceContainer;
            baseEntity.Position = position;
            
            //@TODO: bingref
            //baseEntity.DoBindRef();
            
            // if (baseEntity is Entity entity) {
            //     PhysicSystem.Instance.RegisterEntity(prefabId, entity);
            // }

            baseEntity.DoAwake();
            baseEntity.DoStart();
            
            //@TODO: view
            //_gameViewService.BindView(baseEntity);
            AddEntity(baseEntity);
            return baseEntity;
        }

        public void DestroyEntity(Entity entity){
            RemoveEntity(entity);
        }

        public override void Backup(int toTick){
            if (GameEntry.Instance.IsClientMode) {
                if (m_Tick2Backup.TryGetValue(toTick, out var val)) {
                    var reader = new Deserializer(val.Data);
                    var hash = reader.ReadInt32();
                    if (hash != GameEntry.Instance.CurrentHash) {
                        Debug.LogError(
                            $"Backup data invalid ! CurHash {GameEntry.Instance.CurrentHash} is different from oldHash {val}");
                    }
                }
            }

            Serializer writer = new Serializer();
            writer.Write(GameEntry.Instance.CurrentHash); //hash
            BackUpEntities(GetPlayers(), writer);
            // BackUpEntities(GetEnemies(), writer);
            // BackUpEntities(GetSpawners(), writer);
            m_Tick2Backup[toTick] = writer;

            cmdBuffer.Execute(toTick, new CopyStateCmd());
        }

        public override void RollbackTo(int currentTick, int toTick){
            base.RollbackTo(currentTick, toTick);
            if (m_Tick2Backup.TryGetValue(toTick, out var backupData)) {
                //.TODO reduce the unnecessary create and destroy 
                var reader = new Deserializer(backupData.Data);
                var hash = reader.ReadInt32();
                GameEntry.Instance.CurrentHash = hash;

                var oldId2Entity = m_Id2Entities;
                m_Id2Entities = new Dictionary<int, Entity>();
                m_Type2Entities.Clear();

                //. Recover Entities
                RecoverEntities(new List<Player>(), reader);
                // RecoverEntities(new List<Enemy>(), reader);
                // RecoverEntities(new List<Spawner>(), reader);

                //@TODO: 
                //. Rebind Ref
                foreach (var entity in m_Id2Entities.Values) {
                    // entity.GameStateService = _gameStateService;
                    // entity.ServiceContainer = _serviceContainer;
                    // entity.DoBindRef();
                }

                //@TODO:
                //. Rebind Views 
                foreach (var pair in m_Id2Entities) {
                    Entity oldEntity = null;
                    if (oldId2Entity.TryGetValue(pair.Key, out var poldEntity)) {
                        oldEntity = poldEntity;
                        oldId2Entity.Remove(pair.Key);
                    }
                    
                    //_gameViewService.BindView(pair.Value, oldEntity);
                }
                
                //@TODO: 
                //. Unbind Entity views
                // foreach (var pair in oldId2Entity) {
                //     _gameViewService.UnbindView(pair.Value);
                // }
            }
            else {
                Debug.LogError($"Miss backup data  cannot rollback! {toTick}");
            }
        }


        public override void Clear(int currentTick, int targetTick)
        {
            base.Clear(currentTick, targetTick);
        }

        void BackUpEntities<T>(T[] lst, Serializer writer) where T : Entity, new(){
            writer.Write(lst.Length);
            foreach (var item in lst) {
                item.Write(writer);
            }
        }

        List<T> RecoverEntities<T>(List<T> lst, Deserializer reader) where T : Entity, new(){
            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++) {
                var t = new T();
                lst.Add(t);
                t.Read(reader);
            }

            m_Type2Entities[typeof(T)] = lst;
            foreach (var e in lst) {
                m_Id2Entities[e.EntityId] = e;
            }

            return lst;
        }


        public class CopyStateCmd : BaseCommand {
            private FGameState _state;

            public override void Do(object param){
                _state = ((GameStateService) param).m_GameState;
            }

            public override void Undo(object param){
                ((GameStateService) param).m_GameState = _state;
            }
        }


        protected override FuncUndoCommands GetRollbackFunc(){
            return (minTickNode, maxTickNode, param) => { minTickNode.cmd.Undo(param); };
        }

        public struct FGameState {
            public LFloat RemainTime;
            public LFloat DeltaTime;
            public int MaxEnemyCount;
            public int CurEnemyCount;
            public int CurEnemyId;

            public int GetHash(ref int idx){
                int hash = 1;
                hash += CurEnemyCount.GetHash(ref idx) * PrimerLUT.GetPrimer(idx++);
                hash += MaxEnemyCount.GetHash(ref idx) * PrimerLUT.GetPrimer(idx++);
                hash += CurEnemyId.GetHash(ref idx) * PrimerLUT.GetPrimer(idx++);
                return hash;
            }
        }


        public LFloat RemainTime {
            get => m_GameState.RemainTime;
            set => m_GameState.RemainTime = value;
        }

        public LFloat DeltaTime {
            get => m_GameState.DeltaTime;
            set => m_GameState.DeltaTime = value;
        }

        public int MaxEnemyCount {
            get => m_GameState.MaxEnemyCount;
            set => m_GameState.MaxEnemyCount = value;
        }

        public int CurEnemyCount {
            get => m_GameState.CurEnemyCount;
            set => m_GameState.CurEnemyCount = value;
        }

        public int CurEnemyId {
            get => m_GameState.CurEnemyId;
            set => m_GameState.CurEnemyId = value;
        }

        #endregion
        
        
        
    }
