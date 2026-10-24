using System.Windows.Threading;

namespace DynamicIsland.Services;

public enum FocusPhase
{
    Focus,
    ShortBreak,
    LongBreak
}

public sealed class FocusService : IDisposable
{
    private readonly SettingsService _settings;
    private readonly DispatcherTimer _timer;

    private DateTime _endTime;
    private DateTime _sessionDate = DateTime.Today;
    private bool _sessionInProgress;
    private bool _disposed;

    public FocusPhase Phase { get; private set; } = FocusPhase.Focus;
    public TimeSpan Duration { get; private set; }
    public TimeSpan Remaining { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsComplete { get; private set; }
    public int CompletedSessionsToday { get; private set; }

    /// <summary>Timer text should replace the clock in the collapsed pill.</summary>
    public bool ShowInPill => _sessionInProgress;

    /// <summary>There is a running/paused session, or a completed one waiting for a break.</summary>
    public bool HasSession => _sessionInProgress || IsComplete;

    public event Action? Tick;
    public event Action? StateChanged;
    public event Action<FocusPhase>? Completed;

    public FocusService(SettingsService settings)
    {
        _settings = settings;
        _timer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += (_, _) => OnTimerTick();

        Duration = GetDuration(Phase);
        Remaining = Duration;
        _settings.Changed += OnSettingsChanged;
    }

    public TimeSpan DurationFor(FocusPhase phase) => GetDuration(phase);

    public void Start()
    {
        RefreshDay();

        if (IsRunning)
        {
            return;
        }

        if (IsComplete)
        {
            IsComplete = false;
            Duration = GetDuration(Phase);
            Remaining = Duration;
        }

        if (Remaining <= TimeSpan.Zero)
        {
            Remaining = Duration;
        }

        _endTime = DateTime.Now + Remaining;
        IsRunning = true;
        _sessionInProgress = true;
        _timer.Start();
        StateChanged?.Invoke();
        Tick?.Invoke();
    }

    public void Pause()
    {
        if (!IsRunning)
        {
            return;
        }

        Remaining = _endTime - DateTime.Now;
        if (Remaining < TimeSpan.Zero)
        {
            Remaining = TimeSpan.Zero;
        }

        IsRunning = false;
        _timer.Stop();
        StateChanged?.Invoke();
        Tick?.Invoke();
    }

    public void Toggle()
    {
        if (IsRunning)
        {
            Pause();
        }
        else
        {
            Start();
        }
    }

    public void Reset()
    {
        _timer.Stop();
        IsRunning = false;
        IsComplete = false;
        _sessionInProgress = false;
        Duration = GetDuration(Phase);
        Remaining = Duration;
        StateChanged?.Invoke();
        Tick?.Invoke();
    }

    public void SetPhase(FocusPhase phase)
    {
        _timer.Stop();
        IsRunning = false;
        IsComplete = false;
        _sessionInProgress = false;
        Phase = phase;
        Duration = GetDuration(phase);
        Remaining = Duration;
        StateChanged?.Invoke();
        Tick?.Invoke();
    }

    public FocusPhase SuggestBreakPhase()
    {
        return CompletedSessionsToday > 0 && CompletedSessionsToday % 4 == 0
            ? FocusPhase.LongBreak
            : FocusPhase.ShortBreak;
    }

    /// <summary>Starts the recommended next session: a break after focus, or focus after a break.</summary>
    public void StartSuggestedNext()
    {
        if (IsComplete && Phase != FocusPhase.Focus)
        {
            SetPhase(FocusPhase.Focus);
            Start();
            return;
        }

        SetPhase(SuggestBreakPhase());
        Start();
    }

    private void OnTimerTick()
    {
        if (!IsRunning)
        {
            return;
        }

        RefreshDay();

        var remaining = _endTime - DateTime.Now;
        if (remaining > TimeSpan.Zero)
        {
            Remaining = remaining;
            Tick?.Invoke();
            return;
        }

        Remaining = TimeSpan.Zero;
        IsRunning = false;
        IsComplete = true;
        _sessionInProgress = false;
        _timer.Stop();

        if (Phase == FocusPhase.Focus)
        {
            CompletedSessionsToday++;
        }

        Completed?.Invoke(Phase);
        StateChanged?.Invoke();
        Tick?.Invoke();
    }

    private void RefreshDay()
    {
        if (_sessionDate == DateTime.Today)
        {
            return;
        }

        _sessionDate = DateTime.Today;
        CompletedSessionsToday = 0;
        StateChanged?.Invoke();
    }

    private void OnSettingsChanged()
    {
        if (!_disposed)
        {
            if (!IsRunning && !_sessionInProgress && !IsComplete)
            {
                var duration = GetDuration(Phase);
                if (duration != Duration)
                {
                    Duration = duration;
                    Remaining = duration;
                    Tick?.Invoke();
                }
            }

            StateChanged?.Invoke();
        }
    }

    private TimeSpan GetDuration(FocusPhase phase)
    {
        var minutes = phase switch
        {
            FocusPhase.ShortBreak => _settings.Current.ShortBreakMinutes,
            FocusPhase.LongBreak => _settings.Current.LongBreakMinutes,
            _ => _settings.Current.FocusMinutes
        };

        return TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 180));
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _settings.Changed -= OnSettingsChanged;
    }
}
