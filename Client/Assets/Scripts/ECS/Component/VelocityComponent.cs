using Entitas;

[Game]
public class VelocityComponent : IComponent
{
    public int x;
    public int y;

    // don't be afraid of writing helper accessor
    public void SetValue(int nx, int ny)
    {
        x = nx;
        y = ny;
    }
}