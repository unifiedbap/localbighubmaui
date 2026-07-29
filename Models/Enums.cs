namespace BigLocalHub.Models;

/// <summary>
/// Ported from packages/core/types.ts.
///
/// The TypeScript app models these as string-union types ('Quote scheduled',
/// 'Door hanger', …) and stores those exact strings in Firestore. They are kept
/// as C# string constants rather than enums on purpose: several values contain
/// spaces, and any enum mapping would be one more place for the stored value to
/// drift from what the web and Expo apps write. The stored string IS the
/// contract — see StageLabels for how it gets displayed.
/// </summary>
public static class Modules
{
    public const string Dashboard = "dashboard";
    public const string Calendar  = "calendar";
    public const string Leads     = "leads";
    public const string Jobs      = "jobs";
    public const string Clients   = "clients";
    public const string Gc        = "gc";
    public const string Marketing = "marketing";
    public const string Crm       = "crm";
    public const string Time      = "time";
    public const string Money     = "money";
    public const string Bids      = "bids";
    public const string Expenses  = "expenses";
    public const string Portal    = "portal";
    public const string Agenda    = "agenda";

    public static readonly string[] All =
    [
        Dashboard, Calendar, Leads, Jobs, Clients, Gc, Marketing,
        Crm, Time, Money, Bids, Expenses, Portal, Agenda,
    ];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Dashboard] = "Dashboard",
        [Calendar]  = "Calendar",
        [Leads]     = "Leads",
        [Jobs]      = "Jobs",
        [Clients]   = "Clients",
        [Gc]        = "GC & Contractors",
        [Marketing] = "Marketing",
        [Crm]       = "Cold Call CRM",
        [Time]      = "Time",
        [Money]     = "Money",
        [Bids]      = "Bids",
        [Expenses]  = "Expenses",
        [Portal]    = "Customer Portal",
        [Agenda]    = "Agenda",
    };

    public static string Label(string module) =>
        Labels.TryGetValue(module, out var l) ? l : module;
}

/// <summary>Canonical lead stage order — mirrors LEAD_STATUSES in stageLabels.ts.</summary>
public static class LeadStatuses
{
    public const string New            = "New";
    public const string Contacted      = "Contacted";
    public const string QuoteScheduled = "Quote scheduled";
    public const string Quoted         = "Quoted";
    public const string Won            = "Won";
    public const string Lost           = "Lost";

    public static readonly string[] All =
        [New, Contacted, QuoteScheduled, Quoted, Won, Lost];

    /// <summary>A lead still in play — the definition the Dashboard's "Open Leads" uses.</summary>
    public static bool IsOpen(string? status) =>
        status != Won && status != Lost;
}

/// <summary>Canonical job stage order — mirrors JOB_STATUSES in stageLabels.ts.</summary>
public static class JobStatuses
{
    public const string QuoteScheduled = "Quote scheduled";
    public const string Quoted         = "Quoted";
    public const string Scheduled      = "Scheduled";
    public const string InProgress     = "In Progress";
    public const string Complete       = "Complete";
    public const string Cancelled      = "Cancelled";

    public static readonly string[] All =
        [QuoteScheduled, Quoted, Scheduled, InProgress, Complete, Cancelled];

    /// <summary>Matches the Dashboard's "Active Jobs" count.</summary>
    public static bool IsActive(string? status) =>
        status == Scheduled || status == InProgress;
}

public static class LeadSources
{
    public const string Google     = "Google";
    public const string Angi       = "Angi";
    public const string Referral   = "Referral";
    public const string DoorHanger = "Door hanger";
    public const string Social     = "Social";
    public const string Website    = "Website";
    public const string Other      = "Other";

    public static readonly string[] All =
        [Google, Angi, Referral, DoorHanger, Social, Website, Other];
}

public static class UserRoles
{
    public const string Admin = "admin";
    public const string User  = "user";
}

/// <summary>
/// A user's standing WITHIN their company, kept separate from
/// <see cref="UserRoles"/> on purpose.
///
/// UserDoc.role is platform-level: 'admin' means "runs the whole platform" and
/// carries no companyId. companyRole answers a different question — "does this
/// person run their company's crew" — and collapsing the two into one field
/// makes a platform admin accidentally a manager everywhere, or forces a
/// company manager to be given platform powers.
///
/// Absent means Staff. Every existing user therefore reads as Staff without a
/// migration, which is the safe default: nobody silently gains manager rights.
/// </summary>
public static class CompanyRoles
{
    public const string Manager = "manager";
    public const string Staff   = "staff";

    public static readonly string[] All = [Manager, Staff];

    public static string Label(string? role) =>
        role == Manager ? "Manager" : "Team member";
}

/// <summary>
/// Agenda task statuses. Stored lowercase with underscores ('in_progress'),
/// unlike lead/job statuses which are stored title-case — that inconsistency
/// is in the existing data, so it is mirrored rather than "fixed" here.
/// </summary>
public static class TaskStatuses
{
    public const string Todo       = "todo";
    public const string InProgress = "in_progress";
    public const string Done       = "done";

    public static readonly string[] All = [Todo, InProgress, Done];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Todo]       = "To Do",
        [InProgress] = "In Progress",
        [Done]       = "Done",
    };

    public static string Label(string status) =>
        Labels.TryGetValue(status, out var l) ? l : status;
}

public static class TaskPriorities
{
    public const string Low    = "low";
    public const string Medium = "medium";
    public const string High   = "high";

    public static readonly string[] All = [High, Medium, Low];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Low]    = "Low",
        [Medium] = "Medium",
        [High]   = "High",
    };

    public static string Label(string p) =>
        Labels.TryGetValue(p, out var l) ? l : p;
}
