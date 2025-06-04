// if no feature-set declaration, it comes into UnnamedFeature

using Entitas;

[GameFeature]
public class MovementSystem : SystemBase, IExecuteSystem
{
    public MovementSystem(World world) : base(world)
    {
        
    }
    public void Execute()
    {
        // new API for getting group with all matched entities from context
        var entities = Context<Game>.AllOf<PositionComponent, VelocityComponent>().GetEntities();

        foreach (var e in entities)
        {
            var vel = e.Get<VelocityComponent>();
            var pos = e.Modify<PositionComponent>(); // new API for trigger Monitor/ReactiveSystem

            pos.x += vel.x;
            pos.y += vel.y;
        }
    }
}