using System.Collections.ObjectModel;
using BigLocalHub.Models;
using BigLocalHub.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BigLocalHub.ViewModels;

/// <summary>
/// Time entries grouped by day, newest first — the shape someone checking
/// "what did we log this week" actually wants, rather than a flat list.
/// </summary>
public partial class TimeViewModel : ObservableObject, IDisposable
{
    private readonly SessionService _session;
    private readonly FirestoreRepository _repo;
    private IDisposable? _sub;
    private bool _loaded;

    public TimeViewModel(SessionService session, FirestoreRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    public ObservableCollection<TimeDayGroup> Days { get; } = [];

    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _weekSummary = string.Empty;
    [ObservableProperty] private string _totalSummary = string.Empty;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isEmpty;

    public void Load()
    {
        if (_loaded) return;
        _loaded = true;
        if (string.IsNullOrWhiteSpace(_session.CompanyId)) return;

        _sub = _repo.Watch<TimeEntry>($"companies/{_session.CompanyId}/timeEntries", entries =>
            MainThread.BeginInvokeOnMainThread(() => Rebuild(entries)),
            ex => MainThread.BeginInvokeOnMainThread(() =>
            {
                IsRefreshing = false;
                Error = ex.Message.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase)
                    ? "You don't have access to time entries."
                    : "Couldn't load time entries. Check your connection.";
            }));
    }

    private void Rebuild(IReadOnlyList<TimeEntry> entries)
    {
        Days.Clear();

        var groups = entries
            .GroupBy(e => e.Date)
            .OrderByDescending(g => JobsViewModel.ParseDate(g.Key) ?? DateTime.MinValue);

        foreach (var g in groups)
        {
            var date = JobsViewModel.ParseDate(g.Key);
            var rows = g
                .OrderBy(e => e.StartTime)
                .Select(e => new TimeRow(
                    string.IsNullOrWhiteSpace(e.EmployeeName) ? "Unassigned" : e.EmployeeName,
                    FormatRange(e),
                    $"{e.Hours:0.##} h",
                    string.IsNullOrWhiteSpace(e.Notes) ? string.Empty : e.Notes))
                .ToList();

            Days.Add(new TimeDayGroup(
                date?.ToString("dddd, MMM d") ?? g.Key,
                $"{g.Sum(e => e.Hours):0.##} h",
                rows));
        }

        var total = entries.Sum(e => e.Hours);
        // "This week" uses the last 7 days rather than a calendar week —
        // simpler to reason about mid-week and doesn't depend on locale.
        var weekAgo = DateTime.Today.AddDays(-7);
        var week = entries
            .Where(e => (JobsViewModel.ParseDate(e.Date) ?? DateTime.MinValue) >= weekAgo)
            .Sum(e => e.Hours);

        WeekSummary = $"{week:0.##} h";
        TotalSummary = $"{total:0.##} h";
        IsEmpty = Days.Count == 0;
        IsRefreshing = false;
    }

    private static string FormatRange(TimeEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.StartTime) && string.IsNullOrWhiteSpace(e.EndTime))
            return "No times recorded";
        return $"{Pretty(e.StartTime)} – {Pretty(e.EndTime)}";
    }

    /// <summary>"14:30" → "2:30 PM". Left as-is when it isn't HH:mm.</summary>
    private static string Pretty(string hhmm)
    {
        if (TimeSpan.TryParse(hhmm, out var t))
            return DateTime.Today.Add(t).ToString("h:mm tt");
        return string.IsNullOrWhiteSpace(hhmm) ? "—" : hhmm;
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

public record TimeDayGroup(string DateLabel, string DayTotal, IReadOnlyList<TimeRow> Entries);
public record TimeRow(string EmployeeName, string TimeRange, string Hours, string Notes);
