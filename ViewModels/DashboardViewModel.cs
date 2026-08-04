using System.Collections.ObjectModel;
using BigLocalHub.Models;
using BigLocalHub.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BigLocalHub.ViewModels;

/// <summary>
/// Dashboard — the template screen for the design system.
///
/// Structured around one requirement: a user must see what needs action within
/// about three seconds of opening the app. So the screen leads with a NEEDS
/// ACTION list (sorted most-urgent first) rather than with vanity totals; the
/// counts live below it as context, and recent activity below that.
///
/// Every action row is tappable and deep-links into Leads pre-filtered to the
/// matching status, so "4 leads need first contact" is one tap from the work
/// itself rather than a number you then go hunting for.
/// </summary>
public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly SessionService _session;
    private readonly FirestoreRepository _repo;
    private readonly List<IDisposable> _subs = [];

    private IReadOnlyList<Lead> _leads = [];
    private IReadOnlyList<Job> _jobs = [];
    private IReadOnlyList<Client> _clients = [];

    /// <summary>
    /// A New lead older than this is treated as overdue rather than merely new.
    /// Three working days is the point the cold-call cadence in the web app
    /// considers a first touch late.
    /// </summary>
    private const int OverdueAfterDays = 3;

    private readonly UserPreferences _prefs;
    private readonly SeoHealthScanService _seoScanService;

    public DashboardViewModel(SessionService session, FirestoreRepository repo, UserPreferences prefs, SeoHealthScanService seoScanService)
    {
        _session = session;
        _repo = repo;
        _prefs = prefs;
        _seoScanService = seoScanService;
    }

    /// <summary>
    /// The four customizable shortcut tiles. Defaults to Jobs / Time /
    /// Calendar / Agenda and is overridable per user — see UserPreferences for
    /// why the choice is stored on the device rather than on the user doc.
    /// </summary>
    public ObservableCollection<QuickAction> QuickActions { get; } = [];

    /// <summary>Every module that could occupy a slot, with its current on/off state.</summary>
    public ObservableCollection<QuickActionChoice> QuickActionChoices { get; } = [];

    [ObservableProperty] private bool _isCustomizing;
    [ObservableProperty] private string _customizeHint = string.Empty;

    private string Uid => _session.FirebaseUser?.Uid ?? "anon";

    private void BuildQuickActions()
    {
        var chosen = _prefs.GetQuickActions(Uid, _session.EnabledModules);

        QuickActions.Clear();
        foreach (var key in chosen)
        {
            var m = ModuleRegistry.Get(key);
            QuickActions.Add(new QuickAction(m.Key, m.Label, m.Icon, m.Blurb));
        }

        BuildChoices(chosen);
    }

    private void BuildChoices(IReadOnlyList<string> chosen)
    {
        QuickActionChoices.Clear();
        foreach (var m in ModuleRegistry.AvailableFor(_session.EnabledModules))
        {
            var on = chosen.Contains(m.Key);
            QuickActionChoices.Add(new QuickActionChoice(
                m.Key, m.Label, m.Icon, on,
                on ? Tokens.Palette.AccentTint : Tokens.Palette.Surface,
                on ? Tokens.Palette.Accent : Tokens.Palette.BorderStrong,
                on ? Tokens.Palette.Accent : Tokens.Palette.TextSecondary));
        }

        CustomizeHint = $"Pick up to {UserPreferences.SlotCount}. Tap to add or remove.";
    }

    [RelayCommand]
    private void ToggleCustomizing()
    {
        IsCustomizing = !IsCustomizing;
        if (IsCustomizing) BuildChoices(QuickActions.Select(q => q.Key).ToList());
    }

    /// <summary>
    /// Adds or removes a module from the grid. When all four slots are full,
    /// the oldest choice is dropped rather than silently ignoring the tap —
    /// a tile that does nothing reads as a bug.
    /// </summary>
    [RelayCommand]
    private void ToggleQuickAction(string key)
    {
        var current = QuickActions.Select(q => q.Key).ToList();

        if (current.Contains(key))
        {
            // Never empty the grid completely.
            if (current.Count <= 1) return;
            current.Remove(key);
        }
        else
        {
            if (current.Count >= UserPreferences.SlotCount) current.RemoveAt(0);
            current.Add(key);
        }

        _prefs.SetQuickActions(Uid, current);
        BuildQuickActions();
    }

    [RelayCommand]
    private void ResetQuickActions()
    {
        _prefs.ResetQuickActions(Uid);
        BuildQuickActions();
    }

    [RelayCommand]
    private static async Task OpenQuickActionAsync(QuickAction action)
    {
        if (AppShell.Instance is { } shell) await shell.ShowModuleAsync(action.Key);
    }

    [ObservableProperty] private string _greeting = string.Empty;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private bool _isDemo;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isLoading = true;

    [ObservableProperty] private int _openLeads;
    [ObservableProperty] private int _totalLeads;
    [ObservableProperty] private int _activeJobs;
    [ObservableProperty] private int _totalClients;

    /// <summary>True when there is genuinely nothing to act on.</summary>
    [ObservableProperty] private bool _allCaughtUp;

    public ObservableCollection<ActionItem> NeedsAction { get; } = [];
    public ObservableCollection<LeadRow> RecentLeads { get; } = [];

    // ── SEO Health widget ──────────────────────────────────────────────────
    // Manager-only (same gate as More → SEO Health). One-shot, not a live
    // listener — the score only ever changes on a weekly schedule or a
    // manual scan, never mid-session on its own.
    [ObservableProperty] private bool _showSeoWidget;
    [ObservableProperty] private bool _hasSeoScore;
    [ObservableProperty] private int _seoScore;
    [ObservableProperty] private string _seoScoreLabel = string.Empty;
    [ObservableProperty] private Color _seoScoreInk = Tokens.Palette.Neutral;
    [ObservableProperty] private string _seoLastCheckedText = "Not checked yet";
    [ObservableProperty] private bool _isScanningSeo;
    [ObservableProperty] private string _seoScanButtonText = "Scan now";
    [ObservableProperty] private string? _seoError;

    public void Load()
    {
        Greeting = GreetingForHour(DateTime.Now.Hour);
        CompanyName = _session.Company?.Name ?? string.Empty;
        IsDemo = _session.Company?.Demo ?? false;

        BuildQuickActions();

        var companyId = _session.CompanyId;
        if (string.IsNullOrWhiteSpace(companyId))
        {
            IsLoading = false;
            return;
        }

        var b = $"companies/{companyId}";

        _subs.Add(_repo.Watch<Lead>($"{b}/leads", leads =>
        {
            _leads = leads;
            MainThread.BeginInvokeOnMainThread(Recompute);
        }, ReportError));

        _subs.Add(_repo.Watch<Job>($"{b}/jobs", jobs =>
        {
            _jobs = jobs;
            MainThread.BeginInvokeOnMainThread(Recompute);
        }, ReportError));

        _subs.Add(_repo.Watch<Client>($"{b}/clients", clients =>
        {
            _clients = clients;
            MainThread.BeginInvokeOnMainThread(Recompute);
        }, ReportError));

        ShowSeoWidget = _session.IsManager;
        if (ShowSeoWidget) _ = RefreshSeoWidgetAsync();
    }

    private async Task RefreshSeoWidgetAsync()
    {
        if (string.IsNullOrWhiteSpace(_session.CompanyId)) return;

        try
        {
            var doc = await _repo.GetDocAsync<SeoHealth>($"seoHealth/{_session.CompanyId}");
            ApplySeoDoc(doc);
        }
        catch (Exception ex)
        {
            // A widget failure shouldn't blank out the rest of the Dashboard —
            // the dedicated SEO Health page surfaces the real error message.
            // Still logged, so a silent failure here isn't invisible everywhere.
            System.Diagnostics.Debug.WriteLine($"[seoHealth] dashboard widget refresh failed: {ex}");
            HasSeoScore = false;
        }
    }

    private void ApplySeoDoc(SeoHealth? doc)
    {
        HasSeoScore = doc is not null;
        if (doc is null)
        {
            SeoScore = 0;
            SeoScoreLabel = "No scan yet";
            SeoLastCheckedText = "Tap Scan now to check your site";
            return;
        }

        SeoScore = doc.Score;
        SeoScoreLabel = doc.ScoreLabel;
        var tone = SeoHealthTones.ForScoreLabel(doc.ScoreLabel);
        SeoScoreInk = StatusTones.Ink(tone);
        SeoLastCheckedText = $"Last checked {doc.LastChecked.ToLocalTime():MMM d}";
    }

    [RelayCommand]
    private async Task ScanSeoNowAsync()
    {
        if (IsScanningSeo || string.IsNullOrWhiteSpace(_session.CompanyId)) return;

        IsScanningSeo = true;
        SeoScanButtonText = "Scanning…";
        SeoError = null;
        try
        {
            await _seoScanService.ScanNowAsync(_session.CompanyId);
            await RefreshSeoWidgetAsync();
        }
        catch (Exception ex)
        {
            SeoError = ex.Message;
        }
        finally
        {
            IsScanningSeo = false;
            SeoScanButtonText = "Scan now";
        }
    }

    [RelayCommand]
    private static async Task OpenSeoHealthAsync() =>
        await Shell.Current.Navigation.PushAsync(
            (Page)Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<Views.SeoHealthPage>());

    private void ReportError(Exception ex) => MainThread.BeginInvokeOnMainThread(() =>
    {
        IsLoading = false;
        Error = ex.Message.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase)
            ? "You don't have access to this company's data."
            : "Couldn't load data. Check your connection and pull to refresh.";
    });

    private void Recompute()
    {
        IsLoading = false;
        Error = null;

        TotalLeads = _leads.Count;
        OpenLeads = _leads.Count(l => LeadStatuses.IsOpen(l.Status));
        ActiveJobs = _jobs.Count(j => JobStatuses.IsActive(j.Status));
        TotalClients = _clients.Count;

        BuildNeedsAction();
        BuildRecentLeads();
    }

    private void BuildNeedsAction()
    {
        NeedsAction.Clear();

        // Titles come from the company's own pipeline wording, never hardcoded
        // trade language. This company is an agency, where "Quote scheduled" is
        // displayed as "Meeting scheduled" — a row reading "quotes to prepare"
        // would contradict every other screen in the product.
        var labels = StageLabels.LeadStatusLabels(_session.Company);
        var newLeads = _leads.Where(l => l.Status == LeadStatuses.New).ToList();

        // Overdue is the sharpest signal, so it goes first and is the only red
        // item — if everything is red, nothing is.
        var overdue = newLeads.Where(l => DaysSinceContact(l) >= OverdueAfterDays).ToList();
        if (overdue.Count > 0)
        {
            var oldest = overdue.Max(DaysSinceContact);
            NeedsAction.Add(ActionItem.Create(
                overdue.Count,
                "Overdue for follow-up",
                oldest == 1 ? "Oldest is 1 day old" : $"Oldest is {oldest} days old",
                StatusTone.Danger,
                LeadStatuses.New));
        }

        var fresh = newLeads.Count - overdue.Count;
        if (fresh > 0)
        {
            NeedsAction.Add(ActionItem.Create(
                fresh,
                labels[LeadStatuses.New],
                "Waiting on a first call",
                StatusTone.Warning,
                LeadStatuses.New));
        }

        var toQuote = _leads.Count(l => l.Status == LeadStatuses.QuoteScheduled);
        if (toQuote > 0)
        {
            NeedsAction.Add(ActionItem.Create(
                toQuote,
                labels[LeadStatuses.QuoteScheduled],
                "Ready for you to prepare",
                StatusTone.Warning,
                LeadStatuses.QuoteScheduled));
        }

        var awaiting = _leads.Count(l => l.Status == LeadStatuses.Quoted);
        if (awaiting > 0)
        {
            NeedsAction.Add(ActionItem.Create(
                awaiting,
                labels[LeadStatuses.Quoted],
                "Waiting on their reply",
                StatusTone.Warning,
                LeadStatuses.Quoted));
        }

        AllCaughtUp = NeedsAction.Count == 0;
    }

    private void BuildRecentLeads()
    {
        var labels = StageLabels.LeadStatusLabels(_session.Company);
        RecentLeads.Clear();

        // Stored ascending by createdAt, so the newest are at the end.
        foreach (var l in _leads.Reverse().Take(4))
        {
            var tone = StatusTones.ForLead(l.Status);
            RecentLeads.Add(new LeadRow(
                l.Id,
                l.Name,
                string.IsNullOrWhiteSpace(l.ServiceType) ? "No service type yet" : l.ServiceType,
                labels.TryGetValue(l.Status, out var lab) ? lab : l.Status,
                StatusTones.Ink(tone),
                StatusTones.Tint(tone)));
        }
    }

    /// <summary>
    /// Days since first contact. dateContact is a plain "yyyy-MM-dd" string, so
    /// an unparseable or empty value returns 0 — treated as "not overdue"
    /// rather than guessing, since a bad date shouldn't manufacture a red alert.
    /// </summary>
    private static int DaysSinceContact(Lead lead)
    {
        if (!DateTime.TryParse(lead.DateContact, out var d)) return 0;
        var days = (DateTime.Today - d.Date).Days;
        return days < 0 ? 0 : days;
    }

    private static string GreetingForHour(int hour) => hour switch
    {
        < 12 => "Good morning",
        < 17 => "Good afternoon",
        _    => "Good evening",
    };

    [RelayCommand]
    private static async Task OpenLeadsFilteredAsync(string status)
    {
        // Points the middle tab at Leads with the filter already applied, so an
        // action row lands on the actual work rather than an unfiltered list.
        if (AppShell.Instance is { } shell)
            await shell.ShowModuleAsync(Modules.Leads,
                new Dictionary<string, object> { ["status"] = status });
    }

    [RelayCommand]
    private static async Task OpenLeadsAsync()
    {
        if (AppShell.Instance is { } shell) await shell.ShowModuleAsync(Modules.Leads);
    }

    public void Dispose()
    {
        foreach (var s in _subs) s.Dispose();
        _subs.Clear();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// One row in NEEDS ACTION. Colors are resolved here rather than in XAML
/// converters so the status palette stays in one place (StatusTones).
/// </summary>
public record ActionItem(
    string CountText,
    string Title,
    string Subtitle,
    Color Ink,
    Color Tint,
    string TargetStatus)
{
    public static ActionItem Create(int count, string title, string subtitle, StatusTone tone, string targetStatus)
        => new(count.ToString(), title, subtitle, StatusTones.Ink(tone), StatusTones.Tint(tone), targetStatus);
}

public record LeadRow(
    string Id,
    string Name,
    string ServiceType,
    string StatusLabel,
    Color StatusInk,
    Color StatusTint);

public record QuickAction(string Key, string Label, string Icon, string Blurb);

public record QuickActionChoice(
    string Key,
    string Label,
    string Icon,
    bool IsSelected,
    Color Background,
    Color BorderColor,
    Color TextColor);
