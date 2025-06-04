
using System.Collections.Generic;
using Entitas;


[Game]
public class PlayerLifetimeComponent:IComponent, IUnique
{ 
    public Queue<FPlayerSpawnInfo> WaitingSpawnPlayers = new Queue<FPlayerSpawnInfo>(); 
    public Queue<int> WaitingDespawnPlayers = new Queue<int>();
        
}
