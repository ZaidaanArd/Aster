using Aster.Core.Interfaces;

namespace Aster.Core.System;

public sealed class MutexSingleInstance : ISingleInstance
{
    private readonly Mutex _mutex;

    public MutexSingleInstance(string name)
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
                // Already released.
            }
        }

        _mutex.Dispose();
    }
}
