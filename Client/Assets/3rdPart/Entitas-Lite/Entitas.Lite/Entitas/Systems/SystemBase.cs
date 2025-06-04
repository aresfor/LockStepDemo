
    using Entitas;

    public class SystemBase:ISystem
    {
        protected World World { get; private set; }
        public SystemBase(World world)
        {
            World = world;
        }
    }
