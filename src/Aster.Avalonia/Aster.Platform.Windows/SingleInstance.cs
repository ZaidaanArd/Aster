namespace Aster.Platform.Windows;

public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;

    public SingleInstance(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        IsFirstInstance = createdNew;
    }

    public bool IsFirstInstance { get; }

    public void Dispose()
    {
        if (IsFirstInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex was already released.
            }
        }

        _mutex.Dispose();
    }
}
