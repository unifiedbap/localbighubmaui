using System.Collections.ObjectModel;
using BigLocalHub.Models;
using BigLocalHub.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BigLocalHub.ViewModels;

/// <summary>
/// Client-facing SEO Health Score — a manager-only status view, not a tool.
/// Displays exactly what the weekly computeSeoHealthScore Cloud Function (in
/// the northstarapp repo) last wrote to seoHealth/{companyId}: a score, a
/// trend against the previous check, and the top 2-3 opportunities in plain
/// language. There is deliberately no fix control and no technical
/// drill-down here — that stays in the separate, internal-only audit CLI.
///
/// Reads are one-shot, not a live listener: pull-to-refresh re-reads the same
/// stored document rather than kicking off a fresh scan, so re-scans stay on
/// the Cloud Function's own schedule and don't get triggered per-tap across
/// every company's app.
/// </summary>
public partial class SeoHealthViewModel : ObservableObject, Views.ILoadable
{
    private readonly SessionService _session;
    private readonly FirestoreRepository _repo;
    private readonly SeoHealthScanService _scanService;
    private bool _loaded;

    public SeoHealthViewModel(SessionService session, FirestoreRepository repo, SeoHealthScanService scanService)
    {
        _session = session;
        _repo = repo;
        _scanService = scanService;
    }

    /// <summary>Gates the whole screen. UI convenience only — the real
    /// boundary is firestore.rules' isManager(companyId), same as Manage Team.</summary>
    [ObservableProperty] private bool _isManager;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _hasScore;
    [ObservableProperty] private string? _error;

    [ObservableProperty] private int _score;
    [ObservableProperty] private string _scoreLabel = string.Empty;
    [ObservableProperty] private Color _scoreInk = Tokens.Palette.Neutral;
    [ObservableProperty] private Color _scoreTint = Tokens.Palette.NeutralTint;
    [ObservableProperty] private string _lastCheckedText = string.Empty;

    [ObservableProperty] private bool _hasTrend;
    [ObservableProperty] private string _trendArrow = string.Empty;
    [ObservableProperty] private string _trendText = string.Empty;
    [ObservableProperty] private Color _trendColor = Tokens.Palette.TextSecondary;

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _scanButtonText = "Scan now";

    public ObservableCollection<SeoOpportunityRow> Opportunities { get; } = [];

    public void Load()
    {
        if (_loaded) return;
        _loaded = true;

        IsManager = _session.IsManager;
        if (!IsManager || string.IsNullOrWhiteSpace(_session.CompanyId)) return;

        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(_session.CompanyId)) return;

        IsRefreshing = true;
        Error = null;
        try
        {
            var doc = await _repo.GetDocAsync<SeoHealth>($"seoHealth/{_session.CompanyId}");
            Apply(doc);
        }
        catch (Exception ex)
        {
            Error = ex.Message.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase)
                ? "You don't have access to the SEO Health Score."
                : "Couldn't load your SEO Health Score. Check your connection.";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// Triggers an immediate scan via the scanCompanySeoHealth Cloud Function
    /// instead of waiting for its weekly schedule, then re-reads the doc it
    /// just wrote. Server-side rate-limited; a cooldown or auth failure
    /// surfaces through Error exactly like a failed refresh.
    /// </summary>
    [RelayCommand]
    private async Task ScanNowAsync()
    {
        if (IsScanning || string.IsNullOrWhiteSpace(_session.CompanyId)) return;

        IsScanning = true;
        ScanButtonText = "Scanning…";
        Error = null;
        try
        {
            await _scanService.ScanNowAsync(_session.CompanyId);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsScanning = false;
            ScanButtonText = "Scan now";
        }
    }

    private void Apply(SeoHealth? doc)
    {
        Opportunities.Clear();
        HasScore = doc is not null;

        if (doc is null)
        {
            Score = 0;
            ScoreLabel = string.Empty;
            LastCheckedText = string.Empty;
            HasTrend = false;
            return;
        }

        Score = doc.Score;
        ScoreLabel = doc.ScoreLabel;

        var tone = SeoHealthTones.ForScoreLabel(doc.ScoreLabel);
        ScoreInk = StatusTones.Ink(tone);
        ScoreTint = StatusTones.Tint(tone);

        LastCheckedText = doc.LastChecked is { } checkedAt
            ? $"Last checked {checkedAt.ToLocalTime():MMM d}"
            : "Not checked yet";

        if (doc.PreviousScore is { } prev)
        {
            var delta = doc.Score - prev;
            HasTrend = true;
            switch (Math.Sign(delta))
            {
                case > 0:
                    TrendArrow = "▲";
                    TrendText = $"Up {delta} since last check";
                    TrendColor = Tokens.Palette.Success;
                    break;
                case < 0:
                    TrendArrow = "▼";
                    TrendText = $"Down {-delta} since last check";
                    TrendColor = Tokens.Palette.Danger;
                    break;
                default:
                    TrendArrow = "—";
                    TrendText = "No change since last check";
                    TrendColor = Tokens.Palette.TextSecondary;
                    break;
            }
        }
        else
        {
            HasTrend = false;
        }

        // Server already ranks and trims to the top 2-3; Take(3) here is a
        // display-side belt-and-suspenders, not the real limit.
        foreach (var o in doc.TopOpportunities.Take(3))
        {
            var impactTone = SeoHealthTones.ForImpact(o.Impact);
            Opportunities.Add(new SeoOpportunityRow(
                o.Title,
                o.PlainLanguageExplanation,
                SeoImpacts.Label(o.Impact),
                StatusTones.Ink(impactTone),
                StatusTones.Tint(impactTone)));
        }
    }
}

public record SeoOpportunityRow(
    string Title,
    string Explanation,
    string ImpactLabel,
    Color ImpactInk,
    Color ImpactTint);
