using System.Collections.ObjectModel;
using BigLocalHub.Models;
using BigLocalHub.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BigLocalHub.ViewModels;

/// <summary>
/// Time — clock in/out plus a month calendar of hours.
///
/// A shift is a TimeEntry with a start and no end. That needs no new field:
/// clocking in writes startTime with endTime empty and hours 0, and clocking
/// out fills both in. An in-progress shift therefore contributes 0 to every
/// total (including the web app's QuickBooks export) until it is closed, which
/// is the correct behaviour — you can't bill hours nobody has finished.
///
/// Only a user whose /users doc is linked to an employee record can clock in,
/// because that link is the only thing proving whose shift it is. Unlinked
/// users get a clear explanation rather than a dead button.
/// </summary>
public partial class TimeViewModel : ObservableObject, IDisposable, Views.ILoadable
{
    private readonly SessionService _session;
    private readonly FirestoreRepository _repo;
    private readonly List<IDisposable> _subs = [];
    private IDispatcherTimer? _ticker;
    private bool _loaded;

    private IReadOnlyList<TimeEntry> _allEntries = [];
    private IReadOnlyList<Employee> _employees = [];
    private Employee? _me;
    private TimeEntry? _openShift;

    public TimeViewModel(SessionService session, FirestoreRepository repo)
    {
        _session = session;
        _repo = repo;
        VisibleMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    }

    private string EntriesPath  => $"companies/{_session.CompanyId}/timeEntries";
    private string EmployeePath => $"companies/{_session.CompanyId}/employees";

    // ── Clock card ──────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isClockedIn;
    [ObservableProperty] private bool _canClock;
    /// <summary>
    /// Whether to render the Clock In button at all. An unlinked user gets no
    /// button rather than a disabled one — MAUI's default disabled styling
    /// barely dims a Button with a custom BackgroundColor, so it still reads
    /// as tappable, and the card's text already explains what to do instead.
    /// </summary>
    [ObservableProperty] private bool _showClockIn;
    [ObservableProperty] private string _clockStatus = string.Empty;
    [ObservableProperty] private string _clockDetail = string.Empty;
    [ObservableProperty] private string _elapsed = string.Empty;
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string? _error;

    // ── Calendar ────────────────────────────────────────────────────────────
    [ObservableProperty] private DateTime _visibleMonth;
    [ObservableProperty] private string _monthLabel = string.Empty;
    [ObservableProperty] private string _monthTotal = string.Empty;
    [ObservableProperty] private string _weekTotal = string.Empty;
    [ObservableProperty] private bool _showingEveryone;
    [ObservableProperty] private string _scopeLabel = "MY HOURS";
    [ObservableProperty] private bool _canSeeEveryone;

    /// <summary>Six rows of seven, so the grid height doesn't jump between months.</summary>
    public ObservableCollection<CalendarCell> Cells { get; } = [];

    // ── Selected day ────────────────────────────────────────────────────────
    [ObservableProperty] private string _selectedDayLabel = string.Empty;
    [ObservableProperty] private bool _hasSelection;
    public ObservableCollection<TimeRow> SelectedDayEntries { get; } = [];

    private DateTime? _selectedDate;

    public void Load()
    {
        if (_loaded) return;
        _loaded = true;

        CanSeeEveryone = _session.IsManager;
        if (string.IsNullOrWhiteSpace(_session.CompanyId)) return;

        _subs.Add(_repo.Watch<Employee>(EmployeePath, emps =>
        {
            _employees = emps;
            MainThread.BeginInvokeOnMainThread(Recompute);
        }, ReportError, orderByField: "name"));

        _subs.Add(_repo.Watch<TimeEntry>(EntriesPath, entries =>
        {
            _allEntries = entries;
            MainThread.BeginInvokeOnMainThread(Recompute);
        }, ReportError));

        // One tick a minute is enough for an "h m" readout and costs nothing.
        _ticker = Application.Current?.Dispatcher.CreateTimer();
        if (_ticker is not null)
        {
            _ticker.Interval = TimeSpan.FromSeconds(30);
            _ticker.Tick += (_, _) => UpdateElapsed();
            _ticker.Start();
        }
    }

    private void ReportError(Exception ex) => MainThread.BeginInvokeOnMainThread(() =>
        Error = ex.Message.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase)
            ? "You don't have access to time entries."
            : "Couldn't load time entries. Check your connection.");

    private void Recompute()
    {
        _me = _employees.FirstOrDefault(e => e.Uid == _session.Uid);
        CanClock = _me is not null;

        _openShift = _me is null
            ? null
            : _allEntries.FirstOrDefault(e => e.EmployeeId == _me.Id && IsOpen(e));

        IsClockedIn = _openShift is not null;
        ShowClockIn = CanClock && !IsClockedIn;

        if (_me is null)
        {
            ClockStatus = "Not set up for time tracking";
            ClockDetail = "Your login isn't linked to a team member yet. A manager can link it under More → Manage Team.";
        }
        else if (_openShift is not null)
        {
            ClockStatus = "On the clock";
            ClockDetail = $"Started at {Pretty(_openShift.StartTime)}";
        }
        else
        {
            ClockStatus = "Not clocked in";
            ClockDetail = $"Signed in as {_me.Name}";
        }

        UpdateElapsed();
        BuildCalendar();
        BuildSelectedDay();
    }

    private void UpdateElapsed()
    {
        if (_openShift is null) { Elapsed = string.Empty; return; }

        var started = CombineDateTime(_openShift.Date, _openShift.StartTime);
        if (started is null) { Elapsed = string.Empty; return; }

        var span = DateTime.Now - started.Value;
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        Elapsed = span.TotalHours >= 1
            ? $"{(int)span.TotalHours}h {span.Minutes}m"
            : $"{span.Minutes}m";
    }

    // ── Scope ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleScope()
    {
        if (!CanSeeEveryone) return;
        ShowingEveryone = !ShowingEveryone;
        ScopeLabel = ShowingEveryone ? "EVERYONE'S HOURS" : "MY HOURS";
        BuildCalendar();
        BuildSelectedDay();
    }

    private IEnumerable<TimeEntry> InScope() =>
        ShowingEveryone || _me is null
            ? _allEntries
            : _allEntries.Where(e => e.EmployeeId == _me.Id);

    // ── Calendar ────────────────────────────────────────────────────────────

    [RelayCommand]
    private void PreviousMonth() { VisibleMonth = VisibleMonth.AddMonths(-1); BuildCalendar(); }

    [RelayCommand]
    private void NextMonth() { VisibleMonth = VisibleMonth.AddMonths(1); BuildCalendar(); }

    [RelayCommand]
    private void SelectDay(CalendarCell cell)
    {
        if (!cell.InMonth) return;
        _selectedDate = cell.Date;
        BuildCalendar();
        BuildSelectedDay();
    }

    private void BuildCalendar()
    {
        MonthLabel = VisibleMonth.ToString("MMMM yyyy");

        var byDay = InScope()
            .Where(e => e.Hours > 0)
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Hours));

        // Start on the Sunday on or before the 1st, matching the web calendar.
        var first = new DateTime(VisibleMonth.Year, VisibleMonth.Month, 1);
        var start = first.AddDays(-(int)first.DayOfWeek);

        Cells.Clear();
        for (var i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            var key = date.ToString("yyyy-MM-dd");
            byDay.TryGetValue(key, out var hours);

            var inMonth = date.Month == VisibleMonth.Month;
            var isToday = date == DateTime.Today;
            var selected = _selectedDate == date;

            Cells.Add(new CalendarCell(
                date,
                date.Day.ToString(),
                inMonth,
                hours > 0 ? $"{hours:0.#}h" : string.Empty,
                hours > 0,
                selected ? Tokens.Palette.Accent
                    : isToday ? Tokens.Palette.AccentTint
                    : Colors.Transparent,
                selected ? Colors.White
                    : !inMonth ? Tokens.Palette.TextTertiary
                    : Tokens.Palette.TextPrimary,
                selected ? Colors.White : Tokens.Palette.Accent));
        }

        var monthHours = InScope()
            .Where(e => ParseDate(e.Date)?.Month == VisibleMonth.Month
                     && ParseDate(e.Date)?.Year == VisibleMonth.Year)
            .Sum(e => e.Hours);
        MonthTotal = $"{monthHours:0.##} h";

        var weekAgo = DateTime.Today.AddDays(-7);
        WeekTotal = $"{InScope().Where(e => (ParseDate(e.Date) ?? DateTime.MinValue) >= weekAgo).Sum(e => e.Hours):0.##} h";
    }

    private void BuildSelectedDay()
    {
        SelectedDayEntries.Clear();
        HasSelection = _selectedDate is not null;
        if (_selectedDate is null) return;

        SelectedDayLabel = _selectedDate.Value.ToString("dddd, MMM d");
        var key = _selectedDate.Value.ToString("yyyy-MM-dd");

        foreach (var e in InScope().Where(e => e.Date == key).OrderBy(e => e.StartTime))
        {
            SelectedDayEntries.Add(new TimeRow(
                string.IsNullOrWhiteSpace(e.EmployeeName) ? "Unassigned" : e.EmployeeName,
                IsOpen(e) ? $"{Pretty(e.StartTime)} – in progress" : $"{Pretty(e.StartTime)} – {Pretty(e.EndTime)}",
                IsOpen(e) ? "—" : $"{e.Hours:0.##} h",
                e.Notes ?? string.Empty));
        }
    }

    // ── Clock actions ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ClockInAsync()
    {
        if (Busy || _me is null || IsClockedIn) return;

        Busy = true;
        Error = null;
        try
        {
            await _repo.AddAsync(EntriesPath, new TimeEntry
            {
                EmployeeId = _me.Id,
                EmployeeName = _me.Name,
                Date = DateTime.Today.ToString("yyyy-MM-dd"),
                StartTime = DateTime.Now.ToString("HH:mm"),
                // Empty end + zero hours IS the open-shift marker.
                EndTime = string.Empty,
                Hours = 0,
                Notes = string.Empty,
            });
        }
        catch (Exception ex)
        {
            Error = $"Couldn't clock in: {ex.Message}";
        }
        finally { Busy = false; }
    }

    [RelayCommand]
    private async Task ClockOutAsync()
    {
        if (Busy || _openShift is null) return;

        Busy = true;
        Error = null;
        try
        {
            var end = DateTime.Now;
            await _repo.UpdateAsync(EntriesPath, _openShift.Id,
                ("endTime", end.ToString("HH:mm")),
                ("hours", Math.Round(HoursBetween(_openShift.StartTime, end.ToString("HH:mm")), 2)));
        }
        catch (Exception ex)
        {
            Error = $"Couldn't clock out: {ex.Message}";
        }
        finally { Busy = false; }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>A shift with a start but no end is still running.</summary>
    private static bool IsOpen(TimeEntry e) =>
        !string.IsNullOrWhiteSpace(e.StartTime) && string.IsNullOrWhiteSpace(e.EndTime);

    /// <summary>
    /// Hours between two "HH:mm" values. A end earlier than start means the
    /// shift ran past midnight, so a day is added rather than recording
    /// negative hours.
    /// </summary>
    internal static double HoursBetween(string start, string end)
    {
        if (!TimeSpan.TryParse(start, out var s) || !TimeSpan.TryParse(end, out var e)) return 0;
        var span = e - s;
        if (span < TimeSpan.Zero) span += TimeSpan.FromDays(1);
        return span.TotalHours;
    }

    private static DateTime? CombineDateTime(string date, string time)
    {
        if (!DateTime.TryParse(date, out var d)) return null;
        if (!TimeSpan.TryParse(time, out var t)) return null;
        return d.Date.Add(t);
    }

    private static DateTime? ParseDate(string raw) =>
        DateTime.TryParse(raw, out var d) ? d.Date : null;

    private static string Pretty(string hhmm) =>
        TimeSpan.TryParse(hhmm, out var t)
            ? DateTime.Today.Add(t).ToString("h:mm tt")
            : string.IsNullOrWhiteSpace(hhmm) ? "—" : hhmm;

    public void Dispose()
    {
        _ticker?.Stop();
        _ticker = null;
        foreach (var s in _subs) s.Dispose();
        _subs.Clear();
        GC.SuppressFinalize(this);
    }
}

public record CalendarCell(
    DateTime Date,
    string DayNumber,
    bool InMonth,
    string HoursLabel,
    bool HasHours,
    Color Background,
    Color TextColor,
    Color HoursColor);

public record TimeRow(string EmployeeName, string TimeRange, string Hours, string Notes);
