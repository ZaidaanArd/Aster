using Aster.Core.Interfaces;

namespace Aster.Core.System;

public sealed class MutexSingleInstance : ISingleInstance
{
    private readonly Mutex? _mutex;
    private readonly FileStream? _lockFile;

    public MutexSingleInstance(string name)
    {
        if (OperatingSystem.IsWindows())
        {
            _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
            IsFirstInstance = createdNew;
            return;
        }

        // Named mutexes are not shared across NativeAOT processes on Unix.
        // An exclusive file lock provides the same process-wide guarantee.
        Directory.CreateDirectory(AppPaths.BaseDirectory);
        var safeName = new string(name
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray());
        var path = Path.Combine(AppPaths.BaseDirectory, safeName + ".lock");
        try
        {
            _lockFile = new FileStream(
                path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            IsFirstInstance = true;
        }
        catch (IOException)
        {
            IsFirstInstance = false;
        }
    }

    public bool IsFirstInstance { get; }

    public void Dispose()
    {
        if (_mutex is not null)
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

        _lockFile?.Dispose();
    }
}
