
    using Entitas.VisualDebugging.Unity;
    using UnityEngine;

    public class Feature:DebugSystems
    {
        public Feature(string name) : base(name)
        {
            
        }
        

        public override void Initialize()
        {
            base.Initialize();
            
            Object.DontDestroyOnLoad(gameObject);
        }


        protected Feature(bool noInit) : base(noInit)
        {
        }
    }
