namespace ECS
{
    public interface ILifetime
    {
        //void DoAwake(IServiceContainer services);
        
        //@TODO: service inject
        void DoAwake();
        void DoStart();
        void DoDestroy();
        void OnApplicationQuit();
    }
}