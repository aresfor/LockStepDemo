using Entitas;
using Entitas.VisualDebugging.Unity;
using UnityEngine;

public class GameController : MonoBehaviour
{
    private Systems _feature;

    public void Start()
    {
        var contexts = Contexts.sharedInstance;

#if UNITY_EDITOR
        ContextObserverHelper.ObserveAll(contexts);
#endif

        // create random entity
        var rand = new System.Random();
        var context = Contexts.Default;
        var e = context.CreateEntity();
        e.Add<PositionComponent>();
        e.Add<VelocityComponent>().SetValue(rand.Next()%10, rand.Next()%10);

#if UNITY_EDITOR
        _feature = FeatureObserverHelper.CreateFeature(null);
#else
		// init systems, auto collect matched systems, no manual Systems.Add(ISystem) required
		_feature = new Feature(null);
#endif
        _feature.Initialize();
    }

    public void Update()
    {
        _feature.Execute();
        _feature.Cleanup();
    }
}