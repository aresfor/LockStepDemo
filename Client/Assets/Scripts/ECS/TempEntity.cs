
    using Lockstep.Math;
    using Lockstep.Serialization;
    using UnityEngine;

    public class TempEntity:ITempEntity,IEntityState
    {
        public int EntityId { get; set; }
        public LVector3 Position;
        public LVector3 Euler;

        public virtual void DoAwake()
        {
        }

        public virtual void DoStart()
        {
        }

        public virtual void DoDestroy()
        {
        }

        public virtual void OnApplicationQuit()
        {
        }

        public virtual void DoBindRef()
        {
            
        }
        public virtual void AfterSerialize()
        {
            
        }
        public virtual void Write(Serializer writer)
        {
            writer.Write(EntityId);
            writer.Write(Position);
            writer.Write(Euler);
        }

        public virtual void Read(Deserializer reader)
        {
            EntityId = reader.ReadInt32();
            Position = reader.ReadLVector3();
            Euler = reader.ReadLVector3();
        }

        
    }
