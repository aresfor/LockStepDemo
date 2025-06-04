// Sample view just display Entity's Position if changed


using System.Collections.Generic;
using Entitas;
using UnityEngine;
using Entity = Entitas.Entity;

[GameFeature]
public class ViewSystem : ReactiveSystem
{
    public ViewSystem(World world) : base(world)
    {
        // new API, add monitor that watch Position changed and call Process 
        monitors += Context<Default>.AllOf<PositionComponent>().OnAdded(Process);
    }

    protected void Process(List<Entitas.Entity> entities)
    {
        foreach (var e in entities)
        {
            var pos = e.Get<PositionComponent>();
            Debug.Log("Entity" + e.creationIndex + ": x=" + pos.x + " y=" + pos.y);
        }
    }
}