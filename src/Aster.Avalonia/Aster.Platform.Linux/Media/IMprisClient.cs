namespace Aster.Platform.Linux.Media;

public interface IMprisClient
{
    Task<IReadOnlyList<string>> ListPlayersAsync(CancellationToken ct = default);
    Task<MprisPlayerState?> GetStateAsync(string busName, CancellationToken ct = default);
    Task PlayPauseAsync(string busName, CancellationToken ct = default);
    Task NextAsync(string busName, CancellationToken ct = default);
    Task PreviousAsync(string busName, CancellationToken ct = default);
    Task SetPositionAsync(string busName, string trackId, TimeSpan position, CancellationToken ct = default);
    Task SeekAsync(string busName, TimeSpan offset, CancellationToken ct = default);
}
