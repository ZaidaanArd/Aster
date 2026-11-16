using Aster.Core.Interfaces;
using Aster.Platform.Linux.Media;

namespace Aster.Platform.Linux.Tests;

public class MprisDbusIntegrationTests
{
    [Fact]
    public async Task ReadsFakePlayerAndNextChangesTrack()
    {
        if (Environment.GetEnvironmentVariable("ASTER_FAKE_MPRIS") != "1")
        {
            return;
        }

        using var session = new MprisMediaSession(
            new DBusMprisClient(), new InlineDispatcher(), new NullLog());
        session.Start();

        await WaitUntilAsync(() => session.Current.Title == "Tone 1");
        await session.NextAsync();
        await WaitUntilAsync(() => session.Current.Title == "Tone 2");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.True(condition());
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
