using System.Collections.ObjectModel;
using BigLocalHub.Models;
using BigLocalHub.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BigLocalHub.ViewModels;

/// <summary>
/// Port of apps/web/src/pages/Leads.tsx (list, filter chips, add/edit/delete).
/// The spreadsheet-import flow from that page is deliberately not part of this
/// first pass — see README.
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

    /// <summary>"All" plus each canonical status, shown with this company's wording.</summary>
    public ObservableCollection<FilterChip> Filters { get; } = [];

    [ObservableProperty] private string _selectedFilter = "All";
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isEmpty;

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

    private string? _editingId;

    public string[] AllSources => LeadSources.All;
    public string[] AllStatuses => LeadStatuses.All;

    private string CollectionPath => $"companies/{_session.CompanyId}/leads";

    public void Load()
    {
        BuildFilters();

        if (string.IsNullOrWhiteSpace(_session.CompanyId)) return;

        _sub = _repo.Watch<Lead>(CollectionPath, leads =>
        {
            _all = leads;
            MainThread.BeginInvokeOnMainThread(ApplyFilter);
        }, ex => MainThread.BeginInvokeOnMainThread(() =>
        {
            Error = ex.Message.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase)
                ? "You don't have access to these leads."
                : "Couldn't load leads. Check your connection.";
        }));
    }

    private void BuildFilters()
    {
        var labels = StageLabels.LeadStatusLabels(_session.Company);
        Filters.Clear();
        Filters.Add(new FilterChip("All", "All"));
        foreach (var s in LeadStatuses.All)
            Filters.Add(new FilterChip(s, labels.TryGetValue(s, out var l) ? l : s));
    }

    partial void OnSelectedFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var labels = StageLabels.LeadStatusLabels(_session.Company);
        var filtered = SelectedFilter == "All"
            ? _all
            : _all.Where(l => l.Status == SelectedFilter).ToList();

        Leads.Clear();
        // Newest first reads better on a phone than the stored ascending order.
        foreach (var l in filtered.Reverse())
        {
            Leads.Add(new LeadListItem(
                l.Id,
                l.Name,
                string.IsNullOrWhiteSpace(l.Phone) ? "—" : l.Phone,
                string.IsNullOrWhiteSpace(l.ServiceType) ? "—" : l.ServiceType,
                string.IsNullOrWhiteSpace(l.Location) ? "—" : l.Location,
                string.IsNullOrWhiteSpace(l.DateContact) ? "—" : l.DateContact,
                labels.TryGetValue(l.Status, out var lab) ? lab : l.Status,
                l.Status));
        }

        IsEmpty = Leads.Count == 0;
        IsRefreshing = false;
    }

    [RelayCommand]
    private void SelectFilter(string filter) => SelectedFilter = filter;

    [RelayCommand]
    private void OpenAdd()
    {
        _editingId = null;
        EditorTitle = "Add Lead";
        FormName = FormPhone = FormEmail = FormServiceType = FormLocation = FormNotes = string.Empty;
        FormStatus = LeadStatuses.New;
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
        EditorTitle = "Edit Lead";
        FormName = lead.Name;
        FormPhone = lead.Phone;
        FormEmail = lead.Email;
        FormServiceType = lead.ServiceType;
        FormLocation = lead.Location;
        FormNotes = lead.Notes;
        FormStatus = lead.Status;
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
                    Status = FormStatus,
                    Notes = FormNotes.Trim(),
                });
            }
            else
            {
                // Field-level update, never a whole-document set — this model
                // maps only part of the lead, and a set would wipe the cadence
                // fields, portal links, and import batch id the rest of the
                // system depends on.
                await _repo.UpdateAsync(CollectionPath, _editingId,
                    ("name", FormName.Trim()),
                    ("phone", FormPhone.Trim()),
                    ("email", FormEmail.Trim()),
                    ("source", FormSource),
                    ("serviceType", FormServiceType.Trim()),
                    ("location", FormLocation.Trim()),
                    ("status", FormStatus),
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
    private async Task DeleteAsync(string id)
    {
        var lead = _all.FirstOrDefault(l => l.Id == id);
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
            await _repo.RemoveAsync(CollectionPath, id);
        }
        catch (Exception ex)
        {
            Error = $"Couldn't delete: {ex.Message}";
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
    string Phone,
    string ServiceType,
    string Location,
    string DateContact,
    string StatusLabel,
    string RawStatus);

public record FilterChip(string Value, string Label);
