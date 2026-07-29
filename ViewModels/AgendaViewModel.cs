using System.Collections.ObjectModel;
using BigLocalHub.Models;
using BigLocalHub.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BigLocalHub.ViewModels;

/// <summary>
/// Agenda tasks. Supports the one edit that matters in the field — moving a
/// task's status — without opening a form, since the common action is ticking
/// something off between jobs.
/// </summary>
public partial class AgendaViewModel : ObservableObject, IDisposable
{
    private readonly SessionService _session;
    private readonly FirestoreRepository _repo;
    private IDisposable? _sub;
    private IReadOnlyList<AgendaTask> _all = [];
    private bool _loaded;

    public AgendaViewModel(SessionService session, FirestoreRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    public ObservableCollection<AgendaRow> Items { get; } = [];
    public ObservableCollection<FilterChip> Filters { get; } = [];

    [ObservableProperty] private string _selectedFilter = TaskStatuses.Todo;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _resultSummary = string.Empty;
    [ObservableProperty] private string _emptyMessage = "Nothing here.";
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _busy;

    private string CollectionPath => $"companies/{_session.CompanyId}/tasks";

    public void Load()
    {
        if (_loaded) return;
        _loaded = true;

        BuildFilters();
        if (string.IsNullOrWhiteSpace(_session.CompanyId)) return;

        _sub = _repo.Watch<AgendaTask>(CollectionPath, tasks =>
        {
            _all = tasks;
            MainThread.BeginInvokeOnMainThread(ApplyFilter);
        }, ex => MainThread.BeginInvokeOnMainThread(() =>
        {
            IsRefreshing = false;
            Error = ex.Message.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase)
                ? "You don't have access to the agenda."
                : "Couldn't load tasks. Check your connection.";
        }));
    }

    private void BuildFilters()
    {
        Filters.Clear();
        Filters.Add(Chip("All", "All"));
        foreach (var s in TaskStatuses.All)
            Filters.Add(Chip(s, TaskStatuses.Label(s)));
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
        var filtered = SelectedFilter == "All"
            ? _all
            : _all.Where(t => t.Status == SelectedFilter).ToList();

        // Overdue first, then by due date, then undated. Priority breaks ties,
        // so the top of the list is always the most pressing thing.
        var ordered = filtered
            .OrderBy(t => DueDate(t) is null ? 1 : 0)
            .ThenBy(t => DueDate(t) ?? DateTime.MaxValue)
            .ThenBy(t => PriorityRank(t.Priority));

        Items.Clear();
        foreach (var t in ordered)
        {
            var due = DueDate(t);
            var overdue = due is not null && due < DateTime.Today && t.Status != TaskStatuses.Done;
            var tone = t.Status == TaskStatuses.Done ? StatusTone.Success
                     : overdue                        ? StatusTone.Danger
                     : t.Priority == TaskPriorities.High ? StatusTone.Warning
                     : StatusTone.Neutral;

            Items.Add(new AgendaRow(
                t.Id,
                t.Title,
                BuildDetails(t, due, overdue),
                t.Status == TaskStatuses.Done ? "Done" : TaskStatuses.Label(t.Status),
                StatusTones.Ink(tone),
                StatusTones.Tint(tone),
                t.Status,
                t.Status == TaskStatuses.Done ? "Reopen" : "Mark done"));
        }

        ResultSummary = Items.Count == 1 ? "1 task" : $"{Items.Count} tasks";
        EmptyMessage = SelectedFilter == "All"
            ? "No tasks yet."
            : $"Nothing in \"{TaskStatuses.Label(SelectedFilter)}\".";
        IsRefreshing = false;
    }

    private static string BuildDetails(AgendaTask t, DateTime? due, bool overdue)
    {
        var parts = new List<string>();
        if (due is not null)
            parts.Add(overdue ? $"Due {due:MMM d} — overdue" : $"Due {due:MMM d}");
        if (!string.IsNullOrWhiteSpace(t.Category)) parts.Add(t.Category);
        if (t.Priority == TaskPriorities.High) parts.Add("High priority");
        return parts.Count == 0 ? "No due date" : string.Join("  ·  ", parts);
    }

    private static DateTime? DueDate(AgendaTask t) =>
        DateTime.TryParse(t.DueDate, out var d) ? d : null;

    private static int PriorityRank(string p) => p switch
    {
        TaskPriorities.High => 0,
        TaskPriorities.Medium => 1,
        _ => 2,
    };

    /// <summary>
    /// Toggles between done and to-do. Writes only the status field, leaving
    /// everything else on the task untouched.
    /// </summary>
    [RelayCommand]
    private async Task ToggleDoneAsync(string id)
    {
        if (Busy) return;
        var task = _all.FirstOrDefault(t => t.Id == id);
        if (task is null) return;

        Busy = true;
        try
        {
            var next = task.Status == TaskStatuses.Done ? TaskStatuses.Todo : TaskStatuses.Done;
            await _repo.UpdateAsync(CollectionPath, id, ("status", next));
        }
        catch (Exception ex)
        {
            Error = $"Couldn't update task: {ex.Message}";
        }
        finally
        {
            Busy = false;
        }
    }

    [RelayCommand]
    private void Refresh() => ApplyFilter();

    public void Dispose()
    {
        _sub?.Dispose();
        _sub = null;
        GC.SuppressFinalize(this);
    }
}

public record AgendaRow(
    string Id,
    string Title,
    string Details,
    string StatusLabel,
    Color StatusInk,
    Color StatusTint,
    string RawStatus,
    string ToggleLabel);
