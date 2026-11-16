using Aster.Core.Interfaces;
using Aster.Core.Models;
using Aster.Platform.Linux.Media;

namespace Aster.Platform.Linux.Tests;

public class MprisMediaSessionTests
{
    [Fact]
    public async Task PlayingPlayerWinsOverPaused()
    {
        var client = new FakeClient
        {
            States =
            {
                new MprisPlayerState("org.mpris.MediaPlayer2.spotify", "Spotify",
                    new MprisMetadata("Paused", "", "", TimeSpan.FromMinutes(1), null, "/1"),
                    PlaybackState.Paused, TimeSpan.Zero, true, true, true, DateTimeOffset.UtcNow),
                new MprisPlayerState("org.mpris.MediaPlayer2.vlc", "VLC",
                    new MprisMetadata("Playing", "", "", TimeSpan.FromMinutes(1), null, "/2"),
                    PlaybackState.Playing, TimeSpan.Zero, true, true, true, DateTimeOffset.UtcNow)
            }
        };
        using var session = new MprisMediaSession(client, new InlineDispatcher(), new NullLog());
        session.Start();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (session.Current == MediaSnapshot.Empty && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.Equal("Playing", session.Current.Title);
        Assert.Equal("VLC", session.Current.SourceApp);
    }

    private sealed class FakeClient : IMprisClient
    {
        public List<MprisPlayerState> States { get; } = new();
        public Task<IReadOnlyList<string>> ListPlayersAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(States.Select(s => s.BusName).ToArray());
        public Task<MprisPlayerState?> GetStateAsync(string busName, CancellationToken ct = default) =>
            Task.FromResult(States.FirstOrDefault(s => s.BusName == busName));
        public Task PlayPauseAsync(string busName, CancellationToken ct = default) => Task.CompletedTask;
        public Task NextAsync(string busName, CancellationToken ct = default) => Task.CompletedTask;
        public Task PreviousAsync(string busName, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetPositionAsync(string busName, string trackId, TimeSpan position, CancellationToken ct = default) => Task.CompletedTask;
        public Task SeekAsync(string busName, TimeSpan offset, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;
        public void Post(Action action) => action();
    }

    private sealed class NullLog : IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
