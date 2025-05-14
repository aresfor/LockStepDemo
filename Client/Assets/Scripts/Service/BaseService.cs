
    public abstract class BaseService: IService
    {
        public virtual void DoInit(object objParent){}
        //public virtual void DoAwake(IServiceContainer services){ }
        
        //@TODO: service inject
        public virtual void DoAwake(){ }
        public virtual void DoStart(){ }
        public virtual void DoDestroy(){ }
        public virtual void OnApplicationQuit(){ }
        public virtual int GetHash(ref int idx){return 0;}

        protected CommandBuffer cmdBuffer;
        
        public BaseService()
        {
            cmdBuffer = new CommandBuffer();
            cmdBuffer.Init(this, GetRollbackFunc());
        }
        protected virtual FuncUndoCommands GetRollbackFunc(){
            return null;
        }
        
        public virtual void RollbackTo(int currentTick, int toTick)
        {
            cmdBuffer?.Jump(currentTick, toTick);
        }

        public virtual void Backup(int tick)
        {
        }

        public virtual void Clear(int currentTick, int targetTick)
        {
            cmdBuffer?.Clean(targetTick);

        }

        public virtual int GetHashCode(ref int index)
        {
            return 0;
        }
    }
