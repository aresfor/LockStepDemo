

public interface IRollback
{
    void RollbackTo(int currentTick, int toTick);
}