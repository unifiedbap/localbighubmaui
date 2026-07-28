using System.Collections.ObjectModel;
using BigLocalHub.Models;
using BigLocalHub.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BigLocalHub.ViewModels;

/// <summary>
/// Account + module inventory. Deliberately lists every module the company has
/// enabled alongside whether this MAUI app implements it yet, so the gap
/// between this rendition and the web/Expo apps is visible in the app itself
/// rather than only in the README.
/// </summary>
public partial class MoreViewModel : ObservableObject
{
    private readonly SessionService _session;

    /// <summary>Modules this project actually has a page for.</summary>
    private static readonly HashSet<string> Implemented =
        [Modules.Dashboard, Modules.Leads];

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

    public ObservableCollection<ModuleRow> ModuleRows { get; } = [];

    public void Refresh()
    {
        CompanyName = _session.Company?.Name ?? "—";
        UserName = _session.UserDoc?.Name ?? "—";
        UserEmail = _session.UserDoc?.Email ?? _session.FirebaseUser?.Email ?? "—";
        RoleLabel = _session.IsAdmin ? "Platform admin" : "Team member";

        var style = StageLabels.PipelineStyleFor(_session.Company);
        PipelineStyle = StageLabels.PipelineStyleLabels.TryGetValue(style, out var l) ? l : style;

        ModuleRows.Clear();
        foreach (var m in _session.EnabledModules)
        {
            ModuleRows.Add(new ModuleRow(
                Modules.Label(m),
                Implemented.Contains(m) ? "Available" : "Not in this build yet"));
        }
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

public record ModuleRow(string Label, string Availability);
