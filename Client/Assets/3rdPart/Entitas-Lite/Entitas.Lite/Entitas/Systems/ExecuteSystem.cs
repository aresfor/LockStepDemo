
namespace Entitas
{
	/// Execute on each entity which matches
	public abstract class ExecuteSystem :SystemBase, IExecuteSystem
	{
		protected IMatcher _matcher;

		public ExecuteSystem(World world, IMatcher matcher):base(world)
		{
			_matcher = matcher;
		}

		public virtual void Execute()
		{
			var entities = World.Contexts.GetContext<Game>().GetEntities(_matcher);
			foreach (var e in entities)
			{
				Execute(e);
			}
		}

		protected abstract void Execute(Entity entity);
	}
}