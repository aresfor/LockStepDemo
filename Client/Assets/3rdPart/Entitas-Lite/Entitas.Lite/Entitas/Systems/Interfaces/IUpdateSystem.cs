namespace Entitas
{
    public interface IUpdateSystem:ISystem
    {
        void Update(float deltaTime);
    }
}