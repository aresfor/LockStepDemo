﻿
    using Lockstep.Serialization;

    public interface IRollbackEntity
    {
        public void Read(Deserializer reader);
        public void AfterSerialize();
    }
