using System.Collections.ObjectModel;
using BigLocalHub.Models;
using BigLocalHub.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BigLocalHub.ViewModels;

/// <summary>
/// Leads — list, filter, add/edit/delete. Second consumer of the design system
/// after Dashboard, and the one that proves the card/badge components hold up
/// on a long scrolling list.
///
/// The import flow, cadence engine, and lead→job conversion from the web app
/// are still out of scope here.
/// </summary>
public partial class LeadsViewModel : ObservableObject, IDisposable
{
    private readonly SessionService _session;
    private readonly FirestoreRepository _repo;
    private IDisposable? _sub;
    private IReadOnlyList<Lead> _all = [];

    public LeadsViewModel(SessionService session, FirestoreRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    public ObservableCollection<LeadListItem> Leads { get; } = [];
    public ObservableCollection<FilterChip> Filters { get; } = [];

    [ObservableProperty] private string _selectedFilter = "All";
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _emptyMessage = "No leads yet.";
    [ObservableProperty] private string _resultSummary = string.Empty;

    // ── Editor state ────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isEditorOpen;
    [ObservableProperty] private string _editorTitle = "Add Lead";
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formPhone = string.Empty;
    [ObservableProperty] private string _formEmail = string.Empty;
    [ObservableProperty] private string _formServiceType = string.Empty;
    [ObservableProperty] private string _formLocation = string.Empty;
    [ObservableProperty] private string _formNotes = string.Empty;
    [ObservableProperty] private string _formStatus = LeadStatuses.New;
    [ObservableProperty] private string _formSource = LeadSources.Google;
    [ObservableProperty] private string? _formError;
    [ObservableProperty] private bool _saving;
    [ObservableProperty] private bool _isEditing;

    private string? _editingId;
    private bool _loaded;

    public string[] AllSources => LeadSources.All;

    /// <summary>
    /// Status options for the editor, shown with this company's wording but
    /// bound by index to the canonical values — what gets STORED must stay the
    /// canonical string regardless of how it's labelled.
    /// </summary>
    public ObservableCollection<string> StatusDisplayOptions { get; } = [];

    [ObservableProperty] private int _formStatusIndex;

    private string CollectionPath => $"companies/{_session.CompanyId}/leads";

    public void Load()
    {
        if (_loaded) return;
        _loaded = true;

        var labels = StageLabels.LeadStatusLabels(_session.Company);
        StatusDisplayOptions.Clear();
        foreach (var s in LeadStatuses.All)
            StatusDisplayOptions.Add(labels.TryGetValue(s, out var l) ? l : s);

        BuildFilters();

        if (string.IsNullOrWhiteSpace(_session.CompanyId)) return;

        _sub = _repo.Watch<Lead>(CollectionPath, leads =>
        {
            _all = leads;
            MainThread.BeginInvokeOnMainThread(ApplyFilter);
        }, ex => MainThread.BeginInvokeOnMainThread(() =>
        {
            IsRefreshing = false;
            Error = ex.Message.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase)
                ? "You don't have access to these leads."
                : "Couldn't load leads. Check your connection.";
        }));
    }

    /// <summary>Applied when the Dashboard deep-links in with ?status=…</summary>
    public void ApplyStatusFilter(string status)
    {
        if (LeadStatuses.All.Contains(status) || status == "All")
            SelectedFilter = status;
    }

    private void BuildFilters()
    {
        var labels = StageLabels.LeadStatusLabels(_session.Company);
        Filters.Clear();
        Filters.Add(MakeChip("All", "All"));
        foreach (var s in LeadStatuses.All)
            Filters.Add(MakeChip(s, labels.TryGetValue(s, out var l) ? l : s));
    }

    private FilterChip MakeChip(string value, string label)
    {
        var selected = value == SelectedFilter;
        return new FilterChip(
            value,
            label,
            selected,
            selected ? Tokens.Palette.AccentTint : Tokens.Palette.Surface,
            selected ? Tokens.Palette.Accent : Tokens.Palette.BorderStrong,
            selected ? Tokens.Palette.Accent : Tokens.Palette.TextSecondary,
            selected ? FontAttributes.Bold : FontAttributes.None);
    }

    partial void OnSelectedFilterChanged(string value)
    {
        BuildFilters();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var labels = StageLabels.LeadStatusLabels(_session.Company);
        var filtered = SelectedFilter == "All"
            ? _all
            : _all.Where(l => l.Status == SelectedFilter).ToList();

        Leads.Clear();
        // Newest first — on a phone the top of the list is the cheapest place
        // to look, and the newest lead is the one most likely to need action.
        foreach (var l in filtered.Reverse())
        {
            var tone = StatusTones.ForLead(l.Status);
            Leads.Add(new LeadListItem(
                l.Id,
                l.Name,
                BuildSecondaryLine(l),
                labels.TryGetValue(l.Status, out var lab) ? lab : l.Status,
                StatusTones.Ink(tone),
                StatusTones.Tint(tone)));
        }

        IsEmpty = Leads.Count == 0;
        EmptyMessage = SelectedFilter == "All"
            ? "No leads yet. Tap Add Lead to create the first one."
            : $"No leads with status \"{(labels.TryGetValue(SelectedFilter, out var sl) ? sl : SelectedFilter)}\".";

        ResultSummary = Leads.Count == 1 ? "1 lead" : $"{Leads.Count} leads";
        IsRefreshing = false;
    }

    /// <summary>
    /// One muted line combining the details worth scanning. Built here rather
    /// than stacking three separate labels, which on a small screen turns the
    /// list into a wall of low-contrast text.
    /// </summary>
    private static string BuildSecondaryLine(Lead l)
    {
        var parts = new[] { l.ServiceType, l.Location, l.Phone }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var line = string.Join("  ·  ", parts);
        return string.IsNullOrWhiteSpace(line) ? "No details yet" : line;
    }

    [RelayCommand]
    private void SelectFilter(string filter) => SelectedFilter = filter;

    [RelayCommand]
    private void OpenAdd()
    {
        _editingId = null;
        IsEditing = false;
        EditorTitle = "Add Lead";
        FormName = FormPhone = FormEmail = FormServiceType = FormLocation = FormNotes = string.Empty;
        FormStatus = LeadStatuses.New;
        FormStatusIndex = 0;
        FormSource = LeadSources.Google;
        FormError = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(string id)
    {
        var lead = _all.FirstOrDefault(l => l.Id == id);
        if (lead is null) return;

        _editingId = lead.Id;
        IsEditing = true;
        EditorTitle = "Edit Lead";
        FormName = lead.Name;
        FormPhone = lead.Phone;
        FormEmail = lead.Email;
        FormServiceType = lead.ServiceType;
        FormLocation = lead.Location;
        FormNotes = lead.Notes;
        FormStatus = lead.Status;
        FormStatusIndex = Math.Max(0, Array.IndexOf(LeadStatuses.All, lead.Status));
        FormSource = lead.Source;
        FormError = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void CloseEditor() => IsEditorOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Saving) return;
        if (string.IsNullOrWhiteSpace(FormName))
        {
            FormError = "Name is required.";
            return;
        }

        // The picker binds by index against the display labels; translate back
        // to the canonical stored value.
        var status = FormStatusIndex >= 0 && FormStatusIndex < LeadStatuses.All.Length
            ? LeadStatuses.All[FormStatusIndex]
            : LeadStatuses.New;

        Saving = true;
        FormError = null;
        try
        {
            if (_editingId is null)
            {
                await _repo.AddAsync(CollectionPath, new Lead
                {
                    Name = FormName.Trim(),
                    Phone = FormPhone.Trim(),
                    Email = FormEmail.Trim(),
                    Source = FormSource,
                    ServiceType = FormServiceType.Trim(),
                    Location = FormLocation.Trim(),
                    DateContact = DateTime.Now.ToString("yyyy-MM-dd"),
                    Status = status,
                    Notes = FormNotes.Trim(),
                });
            }
            else
            {
                // Field-level update, never a whole-document set — this model
                // maps only part of the lead, and a set would wipe the cadence
                // fields, portal links, and import batch id.
                await _repo.UpdateAsync(CollectionPath, _editingId,
                    ("name", FormName.Trim()),
                    ("phone", FormPhone.Trim()),
                    ("email", FormEmail.Trim()),
                    ("source", FormSource),
                    ("serviceType", FormServiceType.Trim()),
                    ("location", FormLocation.Trim()),
                    ("status", status),
                    ("notes", FormNotes.Trim()));
            }

            IsEditorOpen = false;
        }
        catch (Exception ex)
        {
            FormError = $"Couldn't save: {ex.Message}";
        }
        finally
        {
            Saving = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_editingId is null) return;
        var lead = _all.FirstOrDefault(l => l.Id == _editingId);
        if (lead is null) return;

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is not null)
        {
            var ok = await page.DisplayAlertAsync(
                "Delete lead",
                $"Delete \"{lead.Name}\"? This cannot be undone.",
                "Delete", "Cancel");
            if (!ok) return;
        }

        try
        {
            await _repo.RemoveAsync(CollectionPath, _editingId);
            IsEditorOpen = false;
        }
        catch (Exception ex)
        {
            FormError = $"Couldn't delete: {ex.Message}";
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

public record LeadListItem(
    string Id,
    string Name,
    string Details,
    string StatusLabel,
    Color StatusInk,
    Color StatusTint);

public record FilterChip(
    string Value,
    string Label,
    bool IsSelected,
    Color Background,
    Color BorderColor,
    Color TextColor,
    FontAttributes Weight);
