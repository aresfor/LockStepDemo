
    using System.Collections.Generic;

    public interface IServiceContainer: IFrameState
    {
        public T GetService<T>() where T : IService;

        public void RegisterService<T, TV>(TV service)
            where T : class, IService 
            where TV : T;

        public List<IService> GetAllService();
    }
