using Aster.Core.Interfaces;
using Aster.Core.Models;

namespace Aster.Platform.Linux.Media;

public sealed class EmptyMediaSession : IMediaSession
{
    public MediaSnapshot Current => MediaSnapshot.Empty;
    public event EventHandler<MediaSnapshot>? Changed
    {
        add { }
        remove { }
    }
    public Task PlayPauseAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task NextAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task PreviousAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task SeekAsync(TimeSpan position, CancellationToken ct = default) => Task.CompletedTask;
    public TimeSpan EstimatedPosition(DateTimeOffset now) => TimeSpan.Zero;
    public void Start() { }
}
