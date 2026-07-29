using BigLocalHub.Models;
using BigLocalHub.Services;
using BigLocalHub.Views;

namespace BigLocalHub;

/// <summary>
/// Three permanent slots: Dashboard · [active module] · More.
///
/// Dashboard and More never move. The middle slot is the "where am I" tab — it
/// starts on Leads and becomes whatever module you open from Quick Actions or
/// the More launcher, so the highlighted tab always matches the screen. That
/// replaces an earlier design where modules were pushed onto the Dashboard
/// tab's stack, which left the bar reading "Dashboard" while the page said
/// "Calendar", with no way back.
///
/// Keeping the bar at three also keeps every target comfortably past the 44pt
/// touch floor no matter how many modules a company enables — the More
/// launcher absorbs the growth instead of the tab bar.
/// </summary>
public partial class AppShell : Shell
{
    public const string ModuleRoute = "module";

    private readonly SessionService _session;
    private readonly IServiceProvider _services;
    private readonly ModuleHostPage _host = new();
    private ShellContent? _moduleTab;

    /// <summary>Convenience accessor for view models that need to switch modules.</summary>
    public static AppShell? Instance => Current as AppShell;

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
        {
            tabs.Items.Add(new ShellContent
            {
                Title = "Dashboard",
                Icon = "icon_dashboard.png",
                Route = "dashboard",
                ContentTemplate = new DataTemplate(() => _services.GetRequiredService<DashboardPage>()),
            });
        }

        // The dynamic slot. ShowModuleAsync swaps its content; only Title and
        // Icon change in the bar itself.
        var initial = DefaultModule();
        if (initial is not null)
        {
            _host.ShowModule(initial, _services);
            _moduleTab = new ShellContent
            {
                Title = initial.Label,
                Icon = initial.Icon,
                Route = ModuleRoute,
                Content = _host,
            };
            tabs.Items.Add(_moduleTab);
        }

        tabs.Items.Add(new ShellContent
        {
            Title = "More",
            Icon = "icon_more.png",
            Route = "more",
            ContentTemplate = new DataTemplate(() => _services.GetRequiredService<MorePage>()),
        });

        Items.Add(tabs);
    }

    /// <summary>
    /// Leads by default, falling back to whatever the company does have, so a
    /// company without Leads still gets a usable middle tab rather than a gap.
    /// </summary>
    private ModuleInfo? DefaultModule()
    {
        var swappable = ModuleRegistry.SwappableFor(_session.EnabledModules).ToList();
        return swappable.FirstOrDefault(m => m.Key == Modules.Leads) ?? swappable.FirstOrDefault();
    }

    /// <summary>
    /// Points the middle tab at <paramref name="moduleKey"/> and selects it.
    /// Dashboard and More are unaffected.
    /// </summary>
    public async Task ShowModuleAsync(string moduleKey, IDictionary<string, object>? args = null)
    {
        var info = ModuleRegistry.Get(moduleKey);
        if (!info.Implemented || _moduleTab is null) return;

        _host.ShowModule(info, _services, args);
        _moduleTab.Title = info.Label;
        _moduleTab.Icon = info.Icon;

        await GoToAsync($"//{ModuleRoute}");
    }
}
