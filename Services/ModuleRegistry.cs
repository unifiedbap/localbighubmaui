using BigLocalHub.Models;

namespace BigLocalHub.Services;

/// <summary>
/// One place describing every module: its label, icon, and navigation route,
/// plus whether this client actually implements it.
///
/// Both the More launcher and the Dashboard's Quick Actions read from here, so
/// adding a module means one entry rather than edits scattered across screens.
/// </summary>
/// <param name="PageType">
/// The ContentPage this module opens, or null when it isn't built here yet.
/// Non-null is what "implemented" actually means, so the two can't disagree.
/// </param>
/// <param name="Blurb">
/// One line describing what you can actually DO in this module, shown under
/// the label on the Dashboard's Quick Actions.
///
/// Keep these honest about what is built today, not what is planned — a tile
/// promising "clock in/out" that opens a read-only list is worse than no blurb
/// at all. Update the wording in the same change that adds the capability.
/// </param>
public record ModuleInfo(string Key, string Label, string Icon, Type? PageType, string Blurb = "")
{
    public bool Implemented => PageType is not null;
}

public static class ModuleRegistry
{
    private static readonly Dictionary<string, ModuleInfo> Map = new()
    {
        [Modules.Dashboard] = new(Modules.Dashboard, "Dashboard", "icon_dashboard.png", typeof(Views.DashboardPage),
            "What needs you today, at a glance"),
        [Modules.Leads]     = new(Modules.Leads,     "Leads",     "icon_leads.png",     typeof(Views.LeadsPage),
            "Add leads, update status, follow up"),
        [Modules.Jobs]      = new(Modules.Jobs,      "Jobs",      "icon_jobs.png",      typeof(Views.JobsPage),
            "See what's scheduled, active, and done"),
        [Modules.Time]      = new(Modules.Time,      "Time",      "icon_time.png",      typeof(Views.TimePage),
            "Review logged hours by day and week"),
        [Modules.Calendar]  = new(Modules.Calendar,  "Calendar",  "icon_calendar.png",  typeof(Views.CalendarPage),
            "What's coming up, and sync to your phone"),
        [Modules.Agenda]    = new(Modules.Agenda,    "Agenda",    "icon_agenda.png",    typeof(Views.AgendaPage),
            "Your task list — tick things off as you go"),

        // Not built in this client yet — listed so they can still be shown
        // (and honestly marked) rather than silently disappearing.
        [Modules.Clients]   = new(Modules.Clients,   "Clients",          "icon_clients.png",   null),
        [Modules.Gc]        = new(Modules.Gc,        "GC & Contractors", "icon_gc.png",        null),
        [Modules.Marketing] = new(Modules.Marketing, "Marketing",        "icon_marketing.png", null),
        [Modules.Crm]       = new(Modules.Crm,       "Cold Call CRM",    "icon_crm.png",       null),
        [Modules.Money]     = new(Modules.Money,     "Money",            "icon_money.png",     null),
        [Modules.Bids]      = new(Modules.Bids,      "Bids",             "icon_bids.png",      null),
        [Modules.Expenses]  = new(Modules.Expenses,  "Expenses",         "icon_expenses.png",  null),
        [Modules.Portal]    = new(Modules.Portal,    "Customer Portal",  "icon_portal.png",    null),
    };

    public static ModuleInfo Get(string key) =>
        Map.TryGetValue(key, out var m)
            ? m
            : new ModuleInfo(key, Modules.Label(key), "icon_more.png", null);

    /// <summary>Modules that are both enabled for the company and built here.</summary>
    public static IEnumerable<ModuleInfo> AvailableFor(IEnumerable<string> enabledModules) =>
        enabledModules.Select(Get).Where(m => m.Implemented);

    /// <summary>
    /// Modules eligible for the shell's dynamic middle tab — everything
    /// available except Dashboard, which owns its own permanent slot.
    /// </summary>
    public static IEnumerable<ModuleInfo> SwappableFor(IEnumerable<string> enabledModules) =>
        AvailableFor(enabledModules).Where(m => m.Key != Modules.Dashboard);
}
