using BigLocalHub.Models;
using BigLocalHub.Services;
using BigLocalHub.Views;

namespace BigLocalHub;

/// <summary>
/// Builds the tab bar from company.enabledModules, mirroring how the web
/// Sidebar and the Expo tab bar both gate on the same field. A module the
/// company doesn't have is not rendered at all — it isn't merely hidden, so
/// there's no route to reach it.
///
/// Only the modules this first pass implements can appear; the rest of
/// enabledModules is ignored until those pages exist.
/// </summary>
public partial class AppShell : Shell
{
    private readonly SessionService _session;
    private readonly IServiceProvider _services;

    public AppShell(SessionService session, IServiceProvider services)
    {
        InitializeComponent();
        _session = session;
        _services = services;
        BuildTabs();
    }

    private void BuildTabs()
    {
        Items.Clear();

        var tabs = new TabBar();

        if (_session.HasModule(Modules.Dashboard))
            tabs.Items.Add(MakeTab("Dashboard", "dashboard", typeof(DashboardPage)));

        if (_session.HasModule(Modules.Leads))
            tabs.Items.Add(MakeTab("Leads", "leads", typeof(LeadsPage)));

        // Always present — it holds sign-out and the module inventory, so the
        // user is never stranded even if the company has no enabled modules.
        tabs.Items.Add(MakeTab("More", "more", typeof(MorePage)));

        Items.Add(tabs);
    }

    private ShellContent MakeTab(string title, string route, Type pageType) => new()
    {
        Title = title,
        Route = route,
        // Resolved through DI so each page gets its injected view model.
        ContentTemplate = new DataTemplate(() => _services.GetRequiredService(pageType)),
    };
}
