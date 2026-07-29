using BigLocalHub.Models;

namespace BigLocalHub.Services;

/// <summary>
/// One place describing every module: its label, icon, and navigation route,
/// plus whether this client actually implements it.
///
/// Both the More launcher and the Dashboard's Quick Actions read from here, so
/// adding a module means one entry rather than edits scattered across screens.
/// </summary>
public record ModuleInfo(string Key, string Label, string Icon, string Route, bool Implemented);

public static class ModuleRegistry
{
    private static readonly Dictionary<string, ModuleInfo> Map = new()
    {
        [Modules.Dashboard] = new(Modules.Dashboard, "Dashboard", "icon_dashboard.png", "//dashboard", true),
        [Modules.Leads]     = new(Modules.Leads,     "Leads",     "icon_leads.png",     "//leads",     true),
        [Modules.Jobs]      = new(Modules.Jobs,      "Jobs",      "icon_jobs.png",      "jobs",        true),
        [Modules.Time]      = new(Modules.Time,      "Time",      "icon_time.png",      "time",        true),
        [Modules.Calendar]  = new(Modules.Calendar,  "Calendar",  "icon_calendar.png",  "calendar",    true),
        [Modules.Agenda]    = new(Modules.Agenda,    "Agenda",    "icon_agenda.png",    "agenda",      true),

        // Not built in this client yet — listed so they can still be shown
        // (and honestly marked) rather than silently disappearing.
        [Modules.Clients]   = new(Modules.Clients,   "Clients",         "icon_clients.png",   "", false),
        [Modules.Gc]        = new(Modules.Gc,        "GC & Contractors","icon_gc.png",        "", false),
        [Modules.Marketing] = new(Modules.Marketing, "Marketing",       "icon_marketing.png", "", false),
        [Modules.Crm]       = new(Modules.Crm,       "Cold Call CRM",   "icon_crm.png",       "", false),
        [Modules.Money]     = new(Modules.Money,     "Money",           "icon_money.png",     "", false),
        [Modules.Bids]      = new(Modules.Bids,      "Bids",            "icon_bids.png",      "", false),
        [Modules.Expenses]  = new(Modules.Expenses,  "Expenses",        "icon_expenses.png",  "", false),
        [Modules.Portal]    = new(Modules.Portal,    "Customer Portal", "icon_portal.png",    "", false),
    };

    public static ModuleInfo Get(string key) =>
        Map.TryGetValue(key, out var m)
            ? m
            : new ModuleInfo(key, Modules.Label(key), "icon_more.png", "", false);

    /// <summary>Modules that are both enabled for the company and built here.</summary>
    public static IEnumerable<ModuleInfo> AvailableFor(IEnumerable<string> enabledModules) =>
        enabledModules.Select(Get).Where(m => m.Implemented && m.Route.Length > 0);
}
