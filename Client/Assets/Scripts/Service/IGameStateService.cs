﻿
    using Lockstep.Math;

    public interface IGameStateService:IService
    {
        public T CreateEntity<T>(int prefabId, LVector3 position, LVector3 euler) where T : TempEntity, new();
        public void DestroyEntity(TempEntity entity);


    }
