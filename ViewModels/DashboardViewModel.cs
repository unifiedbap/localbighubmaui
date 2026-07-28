using System.Collections.ObjectModel;
using BigLocalHub.Models;
using BigLocalHub.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BigLocalHub.ViewModels;

/// <summary>
/// Port of apps/web/src/pages/Dashboard.tsx — four stat tiles plus the two
/// recent lists, all driven by live collection listeners.
/// </summary>
public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly SessionService _session;
    private readonly FirestoreRepository _repo;
    private readonly List<IDisposable> _subs = [];

    private IReadOnlyList<Lead> _leads = [];
    private IReadOnlyList<Job> _jobs = [];
    private IReadOnlyList<Client> _clients = [];

    public DashboardViewModel(SessionService session, FirestoreRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    [ObservableProperty] private int _openLeads;
    [ObservableProperty] private int _totalLeads;
    [ObservableProperty] private int _activeJobs;
    [ObservableProperty] private int _totalJobs;
    [ObservableProperty] private int _totalClients;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private bool _isDemo;
    [ObservableProperty] private string? _error;

    public ObservableCollection<LeadRow> RecentLeads { get; } = [];
    public ObservableCollection<JobRow> RecentJobs { get; } = [];

    public void Load()
    {
        var companyId = _session.CompanyId;
        if (string.IsNullOrWhiteSpace(companyId)) return;

        CompanyName = _session.Company?.Name ?? string.Empty;
        IsDemo = _session.Company?.Demo ?? false;

        var b = $"companies/{companyId}";

        _subs.Add(_repo.Watch<Lead>($"{b}/leads", leads =>
        {
            _leads = leads;
            MainThread.BeginInvokeOnMainThread(RecomputeLeads);
        }, ex => MainThread.BeginInvokeOnMainThread(() => Error = Describe(ex))));

        _subs.Add(_repo.Watch<Job>($"{b}/jobs", jobs =>
        {
            _jobs = jobs;
            MainThread.BeginInvokeOnMainThread(RecomputeJobs);
        }, ex => MainThread.BeginInvokeOnMainThread(() => Error = Describe(ex))));

        _subs.Add(_repo.Watch<Client>($"{b}/clients", clients =>
        {
            _clients = clients;
            MainThread.BeginInvokeOnMainThread(() => TotalClients = _clients.Count);
        }, ex => MainThread.BeginInvokeOnMainThread(() => Error = Describe(ex))));
    }

    private void RecomputeLeads()
    {
        TotalLeads = _leads.Count;
        OpenLeads = _leads.Count(l => LeadStatuses.IsOpen(l.Status));

        var labels = StageLabels.LeadStatusLabels(_session.Company);
        RecentLeads.Clear();
        // Newest last in createdAt-ascending order, so take from the end —
        // same as the web app's [...leads].reverse().slice(0, 5).
        foreach (var l in _leads.Reverse().Take(5))
        {
            RecentLeads.Add(new LeadRow(
                l.Name,
                string.IsNullOrWhiteSpace(l.ServiceType) ? "—" : l.ServiceType,
                labels.TryGetValue(l.Status, out var lab) ? lab : l.Status));
        }
    }

    private void RecomputeJobs()
    {
        TotalJobs = _jobs.Count;
        ActiveJobs = _jobs.Count(j => JobStatuses.IsActive(j.Status));

        var labels = StageLabels.JobStatusLabels(_session.Company);
        RecentJobs.Clear();
        foreach (var j in _jobs.Reverse().Take(5))
        {
            RecentJobs.Add(new JobRow(
                j.JobName,
                string.IsNullOrWhiteSpace(j.ClientName) ? "—" : j.ClientName,
                labels.TryGetValue(j.Status, out var lab) ? lab : j.Status));
        }
    }

    private static string Describe(Exception ex) =>
        ex.Message.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase)
            ? "You don't have access to this company's data."
            : "Couldn't load data. Check your connection.";

    public void Dispose()
    {
        foreach (var s in _subs) s.Dispose();
        _subs.Clear();
        GC.SuppressFinalize(this);
    }
}

public record LeadRow(string Name, string ServiceType, string StatusLabel);
public record JobRow(string JobName, string ClientName, string StatusLabel);
