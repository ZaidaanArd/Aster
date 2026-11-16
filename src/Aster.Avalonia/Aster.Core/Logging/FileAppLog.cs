using Aster.Core.Interfaces;

namespace Aster.Core.Logging;

public sealed class FileAppLog : IAppLog
{
    private readonly string _path;
    private readonly object _gate = new();

    public FileAppLog(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message} {exception}");

    private void Write(string level, string message)
    {
        lock (_gate)
        {
            try
            {
                if (File.Exists(_path) && new FileInfo(_path).Length > 1_000_000)
                {
                    var first = _path + ".1";
                    var second = _path + ".2";
                    if (File.Exists(second))
                    {
                        File.Delete(second);
                    }
                    if (File.Exists(first))
                    {
                        File.Move(first, second);
                    }
                    File.Move(_path, first);
                }

                var line = $"{DateTimeOffset.Now:O} {level} {message}{Environment.NewLine}";
                File.AppendAllText(_path, line);
            }
            catch
            {
                // Logging must never crash the app.
            }
        }
    }
}
