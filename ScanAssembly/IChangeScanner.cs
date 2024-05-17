namespace ScanAssembly;

public interface IChangeScanner<T>
{
    public IEnumerable<ScanChange> GetChangesFrom(T original);
}
