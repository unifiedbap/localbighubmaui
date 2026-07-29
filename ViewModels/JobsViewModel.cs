using System.Collections.ObjectModel;
using BigLocalHub.Models;
using BigLocalHub.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BigLocalHub.ViewModels;

public partial class JobsViewModel : ObservableObject, IDisposable, Views.ILoadable
{
    private readonly SessionService _session;
    private readonly FirestoreRepository _repo;
    private IDisposable? _sub;
    private IReadOnlyList<Job> _all = [];
    private bool _loaded;

    public JobsViewModel(SessionService session, FirestoreRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    public ObservableCollection<JobListItem> Jobs { get; } = [];
    public ObservableCollection<FilterChip> Filters { get; } = [];

    [ObservableProperty] private string _selectedFilter = "All";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _resultSummary = string.Empty;
    [ObservableProperty] private string _emptyMessage = "No jobs yet.";
    [ObservableProperty] private bool _isRefreshing;

    public void Load()
    {
        if (_loaded) return;
        _loaded = true;

        BuildFilters();
        if (string.IsNullOrWhiteSpace(_session.CompanyId)) return;

        _sub = _repo.Watch<Job>($"companies/{_session.CompanyId}/jobs", jobs =>
        {
            _all = jobs;
            MainThread.BeginInvokeOnMainThread(ApplyFilter);
        }, ex => MainThread.BeginInvokeOnMainThread(() =>
        {
            IsRefreshing = false;
            Error = ex.Message.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase)
                ? "You don't have access to these jobs."
                : "Couldn't load jobs. Check your connection.";
        }));
    }

    private void BuildFilters()
    {
        var labels = StageLabels.JobStatusLabels(_session.Company);
        Filters.Clear();
        Filters.Add(Chip("All", "All"));
        foreach (var s in JobStatuses.All)
            Filters.Add(Chip(s, labels.TryGetValue(s, out var l) ? l : s));
    }

    private FilterChip Chip(string value, string label)
    {
        var on = value == SelectedFilter;
        return new FilterChip(value, label, on,
            on ? Tokens.Palette.AccentTint : Tokens.Palette.Surface,
            on ? Tokens.Palette.Accent : Tokens.Palette.BorderStrong,
            on ? Tokens.Palette.Accent : Tokens.Palette.TextSecondary,
            on ? FontAttributes.Bold : FontAttributes.None);
    }

    partial void OnSelectedFilterChanged(string value)
    {
        BuildFilters();
        ApplyFilter();
    }

    [RelayCommand]
    private void SelectFilter(string filter) => SelectedFilter = filter;

    private void ApplyFilter()
    {
        var labels = StageLabels.JobStatusLabels(_session.Company);
        var filtered = SelectedFilter == "All" ? _all : _all.Where(j => j.Status == SelectedFilter).ToList();

        Jobs.Clear();
        foreach (var j in filtered.Reverse())
        {
            var tone = StatusTones.ForJob(j.Status);
            Jobs.Add(new JobListItem(
                j.Id,
                string.IsNullOrWhiteSpace(j.JobName) ? "(untitled job)" : j.JobName,
                BuildDetails(j),
                FormatSchedule(j),
                labels.TryGetValue(j.Status, out var lab) ? lab : j.Status,
                StatusTones.Ink(tone),
                StatusTones.Tint(tone)));
        }

        ResultSummary = Jobs.Count == 1 ? "1 job" : $"{Jobs.Count} jobs";
        EmptyMessage = SelectedFilter == "All"
            ? "No jobs yet."
            : $"No jobs with status \"{(labels.TryGetValue(SelectedFilter, out var sl) ? sl : SelectedFilter)}\".";
        IsRefreshing = false;
    }

    private static string BuildDetails(Job j)
    {
        var parts = new[] { j.ClientName, j.ServiceType, j.JobAddress }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var line = string.Join("  ·  ", parts);
        return string.IsNullOrWhiteSpace(line) ? "No details yet" : line;
    }

    /// <summary>
    /// A one-line schedule summary. Start and end collapse to a single date
    /// when they match, so a one-day job doesn't read "12 Mar – 12 Mar".
    /// </summary>
    private static string FormatSchedule(Job j)
    {
        var start = ParseDate(j.StartDate);
        var end = ParseDate(j.EndDate);

        if (start is null && end is null) return "Not scheduled";
        if (start is not null && end is not null && start.Value.Date != end.Value.Date)
            return $"{start:MMM d} – {end:MMM d}";
        return (start ?? end)!.Value.ToString("MMM d");
    }

    internal static DateTime? ParseDate(string raw) =>
        DateTime.TryParse(raw, out var d) ? d : null;

    [RelayCommand]
    private void Refresh() => ApplyFilter();

    public void Dispose()
    {
        _sub?.Dispose();
        _sub = null;
        GC.SuppressFinalize(this);
    }
}

public record JobListItem(
    string Id,
    string Name,
    string Details,
    string Schedule,
    string StatusLabel,
    Color StatusInk,
    Color StatusTint);
