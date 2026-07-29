using System.Collections.ObjectModel;
using BigLocalHub.Models;
using BigLocalHub.Services;
using BigLocalHub.Services.Calendar;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BigLocalHub.ViewModels;

/// <summary>
/// Calendar — an agenda-style list of upcoming work, plus export to the user's
/// own Apple or Google calendar.
///
/// Events are derived from Jobs exactly as the web Calendar does: a job's
/// quoteDate becomes a quote appointment and its startDate a job start. There
/// is no separate events collection to read, and inventing one here would put
/// this client out of step with the web app.
/// </summary>
public partial class CalendarViewModel : ObservableObject, IDisposable
{
    private readonly SessionService _session;
    private readonly FirestoreRepository _repo;
    private readonly IReadOnlyList<ICalendarBridge> _bridges;
    private IDisposable? _sub;
    private List<CalendarEventDto> _events = [];
    private bool _loaded;

    public CalendarViewModel(
        SessionService session,
        FirestoreRepository repo,
        IEnumerable<ICalendarBridge> bridges)
    {
        _session = session;
        _repo = repo;
        _bridges = bridges.ToList();
    }

    public ObservableCollection<CalendarDayGroup> Days { get; } = [];
    public ObservableCollection<BridgeRow> Bridges { get; } = [];

    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _syncMessage;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _syncing;
    [ObservableProperty] private string _rangeSummary = string.Empty;

    public void Load()
    {
        if (_loaded) return;
        _loaded = true;

        _ = RefreshBridgesAsync();

        if (string.IsNullOrWhiteSpace(_session.CompanyId)) return;

        _sub = _repo.Watch<Job>($"companies/{_session.CompanyId}/jobs", jobs =>
            MainThread.BeginInvokeOnMainThread(() => Rebuild(jobs)),
            ex => MainThread.BeginInvokeOnMainThread(() =>
            {
                IsRefreshing = false;
                Error = ex.Message.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase)
                    ? "You don't have access to the schedule."
                    : "Couldn't load the schedule. Check your connection.";
            }));
    }

    private void Rebuild(IReadOnlyList<Job> jobs)
    {
        var labels = StageLabels.JobStatusLabels(_session.Company);
        var events = new List<CalendarEventDto>();

        foreach (var j in jobs)
        {
            var name = string.IsNullOrWhiteSpace(j.JobName) ? "(untitled job)" : j.JobName;

            if (ParseDate(j.QuoteDate) is DateTime quote)
            {
                var start = ApplyTime(quote, j.QuoteTime);
                var timed = start != quote;
                events.Add(new CalendarEventDto(
                    $"job-{j.Id}-quote",
                    $"{labels[JobStatuses.QuoteScheduled]}: {name}",
                    start,
                    // An untimed quote is all-day; a timed one gets a
                    // conventional one-hour slot so it doesn't land as a
                    // zero-length event that some calendars hide entirely.
                    timed ? start.AddHours(1) : quote,
                    !timed,
                    NullIfBlank(j.JobAddress),
                    NullIfBlank(j.ClientName)));
            }

            if (ParseDate(j.StartDate) is DateTime jobStart)
            {
                var end = ParseDate(j.EndDate) ?? jobStart;
                if (end < jobStart) end = jobStart;
                events.Add(new CalendarEventDto(
                    $"job-{j.Id}-start",
                    name,
                    jobStart,
                    end,
                    true,
                    NullIfBlank(j.JobAddress),
                    NullIfBlank(j.ClientName)));
            }
        }

        _events = events.OrderBy(e => e.Start).ToList();

        // Past work is history, not schedule — the list starts from today so
        // the first thing on screen is the next thing to do.
        var upcoming = _events.Where(e => e.Start.Date >= DateTime.Today).ToList();

        Days.Clear();
        foreach (var g in upcoming.GroupBy(e => e.Start.Date).OrderBy(g => g.Key))
        {
            Days.Add(new CalendarDayGroup(
                FormatDayHeader(g.Key),
                g.Key == DateTime.Today,
                g.OrderBy(e => e.Start).Select(e => new CalendarRow(
                    e.Title,
                    e.IsAllDay ? "All day" : e.Start.ToString("h:mm tt"),
                    e.Location ?? string.Empty,
                    !string.IsNullOrWhiteSpace(e.Location))).ToList()));
        }

        IsEmpty = Days.Count == 0;
        RangeSummary = upcoming.Count == 0
            ? "Nothing scheduled"
            : $"{upcoming.Count} upcoming · {_events.Count} total";
        IsRefreshing = false;
    }

    private static string FormatDayHeader(DateTime d)
    {
        if (d == DateTime.Today) return "Today";
        if (d == DateTime.Today.AddDays(1)) return "Tomorrow";
        return d.ToString("dddd, MMM d");
    }

    private static DateTime? ParseDate(string raw) =>
        DateTime.TryParse(raw, out var d) ? d.Date : null;

    /// <summary>Folds an "HH:mm" string onto a date; returns the bare date if unparseable.</summary>
    private static DateTime ApplyTime(DateTime date, string hhmm) =>
        TimeSpan.TryParse(hhmm, out var t) ? date.Add(t) : date;

    private static string? NullIfBlank(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    // ── External calendars ──────────────────────────────────────────────────

    private async Task RefreshBridgesAsync()
    {
        var rows = new List<BridgeRow>();
        foreach (var b in _bridges)
        {
            var state = await b.GetStateAsync();
            rows.Add(BuildRow(b, state));
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Bridges.Clear();
            foreach (var r in rows) Bridges.Add(r);
        });
    }

    private static BridgeRow BuildRow(ICalendarBridge b, BridgeState state)
    {
        var (status, action, canAct) = state switch
        {
            BridgeState.Ready           => ("Connected", "Sync now", true),
            BridgeState.NeedsPermission => ("Not connected", "Connect", true),
            BridgeState.NotConfigured   => ("Setup needed — see README", "Unavailable", false),
            _                           => ("Not available on this device", "Unavailable", false),
        };

        var tone = state switch
        {
            BridgeState.Ready => StatusTone.Success,
            BridgeState.NeedsPermission => StatusTone.Warning,
            _ => StatusTone.Neutral,
        };

        return new BridgeRow(b.Name, b.Description, status, action, canAct,
            StatusTones.Ink(tone), StatusTones.Tint(tone));
    }

    [RelayCommand]
    private async Task SyncAsync(string bridgeName)
    {
        if (Syncing) return;
        var bridge = _bridges.FirstOrDefault(b => b.Name == bridgeName);
        if (bridge is null) return;

        Syncing = true;
        SyncMessage = null;
        try
        {
            var state = await bridge.GetStateAsync();
            if (state != BridgeState.Ready)
            {
                state = await bridge.ConnectAsync();
                if (state != BridgeState.Ready)
                {
                    SyncMessage = state == BridgeState.NotConfigured
                        ? $"{bridge.Name} isn't set up in this build yet."
                        : $"{bridge.Name} access wasn't granted.";
                    return;
                }
            }

            // Only future events are pushed. Back-filling years of finished
            // jobs into someone's personal calendar is not what "sync" means
            // to them, and it can't be undone easily.
            var toSync = _events.Where(e => e.Start.Date >= DateTime.Today).ToList();
            if (toSync.Count == 0)
            {
                SyncMessage = "Nothing upcoming to sync.";
                return;
            }

            var result = await bridge.ExportAsync(toSync);
            SyncMessage = result.Message
                ?? $"{bridge.Name}: {result.Total} event{(result.Total == 1 ? "" : "s")} synced"
                   + (result.Failed > 0 ? $", {result.Failed} failed" : ".");
        }
        catch (Exception ex)
        {
            SyncMessage = $"Sync failed: {ex.Message}";
        }
        finally
        {
            Syncing = false;
            await RefreshBridgesAsync();
        }
    }

    [RelayCommand]
    private void Refresh() => IsRefreshing = false;

    public void Dispose()
    {
        _sub?.Dispose();
        _sub = null;
        GC.SuppressFinalize(this);
    }
}

public record CalendarDayGroup(string Header, bool IsToday, IReadOnlyList<CalendarRow> Events);
public record CalendarRow(string Title, string TimeLabel, string Location, bool HasLocation);
public record BridgeRow(
    string Name,
    string Description,
    string Status,
    string ActionLabel,
    bool CanAct,
    Color StatusInk,
    Color StatusTint);
