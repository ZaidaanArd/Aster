using System.Globalization;
using System.Windows.Threading;

namespace DynamicIsland.Services;

/// <summary>
/// Tracks a daily water goal and raises a gentle reminder at a fixed interval
/// while the user is inside their configured active hours.
/// </summary>
public sealed class WaterService : IDisposable
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly SettingsService _settings;
    private readonly DispatcherTimer _timer;

    private DateTime _nextReminder = DateTime.MaxValue;
    private bool _suppressed;
    private bool _disposed;

    private bool _lastEnabled;
    private int _lastInterval;
    private int _lastStart;
    private int _lastEnd;
    private int _lastGoal;

    public event Action? ReminderDue;
    public event Action? StateChanged;

    public WaterService(SettingsService settings)
    {
        _settings = settings;

        RefreshDay();
        RememberSchedule();
        Reconfigure();

        _settings.Changed += OnSettingsChanged;

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(20)
        };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    public bool Enabled => _settings.Current.WaterReminderEnabled;

    public int GlassesToday => Math.Clamp(_settings.Current.WaterGlassesToday, 0, 99);

    public int Goal => Math.Clamp(_settings.Current.WaterDailyGoal, 1, 20);

    public bool GoalReached => GlassesToday >= Goal;

    public DateTime? NextReminderAt => _nextReminder == DateTime.MaxValue ? null : _nextReminder;

    public TimeSpan Interval =>
        TimeSpan.FromMinutes(Math.Clamp(_settings.Current.WaterIntervalMinutes, 1, 240));

    /// <summary>Reminders are held while the island is hidden (fullscreen or tray).</summary>
    public bool IsSuppressed
    {
        get => _suppressed;
        set
        {
            if (_suppressed == value)
            {
                return;
            }

            _suppressed = value;
            if (!value)
            {
                Tick();
            }
        }
    }

    public bool LogDrink()
    {
        RefreshDay();

        if (GoalReached)
        {
            StateChanged?.Invoke();
            return false;
        }

        _settings.Current.WaterGlassesToday = GlassesToday + 1;
        _settings.Current.WaterDate = TodayKey();
        _settings.Save();

        _nextReminder = ComputeNextReminder(DateTime.Now);

        StateChanged?.Invoke();
        return true;
    }

    public void ResetToday()
    {
        _settings.Current.WaterGlassesToday = 0;
        _settings.Current.WaterDate = TodayKey();
        _settings.Save();

        Reconfigure();
        StateChanged?.Invoke();
    }

    /// <summary>Shows the reminder immediately, used by the Settings preview button.</summary>
    public void Preview()
    {
        if (!_disposed)
        {
            ReminderDue?.Invoke();
        }
    }

    private void Tick()
    {
        if (_disposed)
        {
            return;
        }

        if (RefreshDay())
        {
            Reconfigure();
            StateChanged?.Invoke();
        }

        if (!Enabled || GoalReached || IsSuppressed)
        {
            return;
        }

        var now = DateTime.Now;
        if (!IsWithinActiveHours(now))
        {
            _nextReminder = NextActiveStart(now) + Interval;
            return;
        }

        if (_nextReminder == DateTime.MaxValue)
        {
            _nextReminder = now + Interval;
            return;
        }

        if (now < _nextReminder)
        {
            return;
        }

        _nextReminder = now + Interval;
        StateChanged?.Invoke();
        ReminderDue?.Invoke();
    }

    private void OnSettingsChanged()
    {
        if (_disposed)
        {
            return;
        }

        var settings = _settings.Current;
        var scheduleChanged =
            settings.WaterReminderEnabled != _lastEnabled ||
            settings.WaterIntervalMinutes != _lastInterval ||
            settings.WaterActiveStartHour != _lastStart ||
            settings.WaterActiveEndHour != _lastEnd ||
            settings.WaterDailyGoal != _lastGoal;

        if (!scheduleChanged)
        {
            return;
        }

        RememberSchedule();
        Reconfigure();
        StateChanged?.Invoke();
    }

    private void RememberSchedule()
    {
        var settings = _settings.Current;
        _lastEnabled = settings.WaterReminderEnabled;
        _lastInterval = settings.WaterIntervalMinutes;
        _lastStart = settings.WaterActiveStartHour;
        _lastEnd = settings.WaterActiveEndHour;
        _lastGoal = settings.WaterDailyGoal;
    }

    private void Reconfigure()
    {
        _nextReminder = ComputeNextReminder(DateTime.Now);
    }

    private DateTime ComputeNextReminder(DateTime now)
    {
        if (!Enabled || GoalReached)
        {
            return DateTime.MaxValue;
        }

        return IsWithinActiveHours(now)
            ? now + Interval
            : NextActiveStart(now) + Interval;
    }

    private bool RefreshDay()
    {
        var today = TodayKey();
        if (string.Equals(_settings.Current.WaterDate, today, StringComparison.Ordinal))
        {
            return false;
        }

        _settings.Current.WaterDate = today;
        _settings.Current.WaterGlassesToday = 0;
        _settings.Save();
        return true;
    }

    private bool IsWithinActiveHours(DateTime now)
    {
        var start = Math.Clamp(_settings.Current.WaterActiveStartHour, 0, 23);
        var end = Math.Clamp(_settings.Current.WaterActiveEndHour, 0, 23);

        if (start == end)
        {
            return true;
        }

        return start < end
            ? now.Hour >= start && now.Hour < end
            : now.Hour >= start || now.Hour < end;
    }

    private DateTime NextActiveStart(DateTime now)
    {
        var start = Math.Clamp(_settings.Current.WaterActiveStartHour, 0, 23);
        var end = Math.Clamp(_settings.Current.WaterActiveEndHour, 0, 23);
        var candidate = now.Date.AddHours(start);

        if (start == end || IsWithinActiveHours(now))
        {
            return candidate;
        }

        if (start < end)
        {
            return now.Hour >= end ? candidate.AddDays(1) : candidate;
        }

        // Overnight window, e.g. 22:00 - 08:00.
        return now.Hour >= end && now.Hour < start
            ? candidate
            : candidate.AddDays(1);
    }

    private static string TodayKey() =>
        DateTime.Today.ToString(DateFormat, CultureInfo.InvariantCulture);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _settings.Changed -= OnSettingsChanged;
    }
}
