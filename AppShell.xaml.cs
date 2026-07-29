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
/// The tab bar is deliberately capped (see MaxPrimaryTabs). Modules beyond the
/// cap are reached through the launcher grid on the More tab rather than by
/// letting the bar grow until the labels truncate — a crowded tab bar is
/// exactly what breaks the "find it in three seconds" goal, and shrinking
/// labels to fit would break the 44pt touch floor.
/// </summary>
public partial class AppShell : Shell
{
    private readonly SessionService _session;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Primary tabs excluding More. Four plus More = five total, the ceiling
    /// from the design brief.
    /// </summary>
    private const int MaxPrimaryTabs = 4;

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

        // Ordered by daily importance, not by the module list's order — the
        // first two slots are what a contractor opens the app for.
        var candidates = new (string Module, string Title, string Icon, Type Page)[]
        {
            (Modules.Dashboard, "Dashboard", "icon_dashboard.png", typeof(DashboardPage)),
            (Modules.Leads,     "Leads",     "icon_leads.png",     typeof(LeadsPage)),
        };

        var added = 0;
        foreach (var c in candidates)
        {
            if (added >= MaxPrimaryTabs) break;
            if (!_session.HasModule(c.Module)) continue;
            tabs.Items.Add(MakeTab(c.Title, c.Module, c.Icon, c.Page));
            added++;
        }

        // Always present: it holds the launcher for every other module plus
        // sign-out, so the user is never stranded even with no modules enabled.
        tabs.Items.Add(MakeTab("More", "more", "icon_more.png", typeof(MorePage)));

        Items.Add(tabs);
    }

    private ShellContent MakeTab(string title, string route, string icon, Type pageType) => new()
    {
        // Title AND Icon together — never icon-only. Shell renders both in the
        // tab bar, which is what keeps the destination readable for users who
        // don't recognise the glyph.
        Title = title,
        Icon = icon,
        Route = route,
        ContentTemplate = new DataTemplate(() => _services.GetRequiredService(pageType)),
    };
}
