using System.Collections.ObjectModel;
using BigLocalHub.Models;
using BigLocalHub.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BigLocalHub.ViewModels;

/// <summary>
/// The app launcher. Every module the company has enabled appears here as an
/// icon + label tile, which is how the tab bar stays capped at five items as
/// Jobs, Bids, Time, Expenses, and Portal come online.
///
/// Modules that aren't built in this MAUI client yet are shown but visibly
/// marked as unavailable rather than hidden — a contractor who uses Bids on
/// the web should find out here that it's coming, not silently wonder where
/// it went.
/// </summary>
public partial class MoreViewModel : ObservableObject
{
    private readonly SessionService _session;

    public MoreViewModel(SessionService session)
    {
        _session = session;
        Refresh();
    }

    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userEmail = string.Empty;
    [ObservableProperty] private string _roleLabel = string.Empty;
    [ObservableProperty] private string _pipelineStyle = string.Empty;
    [ObservableProperty] private string _initials = string.Empty;

    public ObservableCollection<ModuleTile> Tiles { get; } = [];

    public void Refresh()
    {
        CompanyName = _session.Company?.Name ?? "—";
        UserName = string.IsNullOrWhiteSpace(_session.UserDoc?.Name) ? "—" : _session.UserDoc!.Name;
        UserEmail = _session.UserDoc?.Email ?? _session.FirebaseUser?.Email ?? "—";
        RoleLabel = _session.IsAdmin ? "Platform admin" : "Team member";
        Initials = MakeInitials(UserName, UserEmail);

        var style = StageLabels.PipelineStyleFor(_session.Company);
        PipelineStyle = StageLabels.PipelineStyleLabels.TryGetValue(style, out var l) ? l : style;

        // Icons, routes, and availability all come from ModuleRegistry, so a
        // module added there shows up here without another table to update.
        Tiles.Clear();
        foreach (var key in _session.EnabledModules)
        {
            var m = ModuleRegistry.Get(key);
            var available = m.Implemented && m.Route.Length > 0;
            Tiles.Add(new ModuleTile(
                m.Label,
                m.Icon,
                available,
                available ? m.Route : string.Empty,
                available ? string.Empty : "Web only for now",
                // Unavailable tiles are dimmed via color rather than opacity so
                // the label still clears the contrast floor.
                available ? Tokens.Palette.TextPrimary : Tokens.Palette.TextTertiary));
        }
    }

    private static string MakeInitials(string name, string email)
    {
        var source = name is "—" or "" ? email : name;
        if (string.IsNullOrWhiteSpace(source) || source == "—") return "?";

        var parts = source.Split([' ', '.', '@'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
        return (parts[0][..1] + parts[1][..1]).ToUpperInvariant();
    }

    [RelayCommand]
    private static async Task OpenModuleAsync(ModuleTile tile)
    {
        if (!tile.IsAvailable || string.IsNullOrEmpty(tile.Route)) return;
        await Shell.Current.GoToAsync(tile.Route);
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is not null)
        {
            var ok = await page.DisplayAlertAsync("Sign out", "Sign out of Big Local Hub?", "Sign out", "Cancel");
            if (!ok) return;
        }
        await _session.SignOutAsync();
    }
}

public record ModuleTile(
    string Label,
    string Icon,
    bool IsAvailable,
    string Route,
    string Note,
    Color LabelColor);
