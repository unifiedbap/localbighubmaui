using Plugin.Firebase.Firestore;

namespace BigLocalHub.Models;

/// <summary>
/// Firestore document models, ported from packages/core/types.ts.
///
/// Field names in [FirestoreProperty] must match the TypeScript property names
/// exactly — these same documents are written by the web app, the Expo app, and
/// the Cloud Functions, so a rename here silently reads null rather than
/// failing loudly.
///
/// Only the fields this walking skeleton actually uses are mapped. The rest of
/// each document (cadence denormalizations, portal links, Stripe ids, …) stays
/// on the document untouched: Plugin.Firebase only serializes mapped
/// properties, so a partial model reads fine, but note that a full-document
/// overwrite would drop unmapped fields — every write below is either an add or
/// a field-level update for that reason.
/// </summary>
public class FirestoreDocument
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;
}

public class UserDoc : FirestoreDocument
{
    [FirestoreProperty("email")]
    public string Email { get; set; } = string.Empty;

    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>null for platform admins — they belong to no single company.</summary>
    [FirestoreProperty("companyId")]
    public string? CompanyId { get; set; }

    [FirestoreProperty("role")]
    public string Role { get; set; } = UserRoles.User;

    /// <summary>
    /// 'manager' or 'staff'. Absent reads as staff — see CompanyRoles for why
    /// this is separate from Role.
    /// </summary>
    [FirestoreProperty("companyRole")]
    public string? CompanyRole { get; set; }

    public bool IsManager => CompanyRole == CompanyRoles.Manager;

    /// <summary>True for the shared live-demo sandbox account (createDemoSession).</summary>
    [FirestoreProperty("demo")]
    public bool Demo { get; set; }

    [FirestoreProperty("phone")]
    public string? Phone { get; set; }

    public bool IsAdmin => Role == UserRoles.Admin;
}

public class Company : FirestoreDocument
{
    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty("contactEmail")]
    public string ContactEmail { get; set; } = string.Empty;

    [FirestoreProperty("enabledModules")]
    public List<string> EnabledModules { get; set; } = [];

    [FirestoreProperty("accentColor")]
    public string? AccentColor { get; set; }

    [FirestoreProperty("logoUrl")]
    public string? LogoUrl { get; set; }

    [FirestoreProperty("businessType")]
    public string? BusinessType { get; set; }

    [FirestoreProperty("demo")]
    public bool Demo { get; set; }

    /// <summary>Per-company DISPLAY overrides for lead stages — see StageLabels.</summary>
    [FirestoreProperty("leadStatusLabels")]
    public Dictionary<string, string>? LeadStatusLabels { get; set; }

    [FirestoreProperty("jobStatusLabels")]
    public Dictionary<string, string>? JobStatusLabels { get; set; }
}

public class Lead : FirestoreDocument
{
    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty("phone")]
    public string Phone { get; set; } = string.Empty;

    [FirestoreProperty("email")]
    public string Email { get; set; } = string.Empty;

    [FirestoreProperty("source")]
    public string Source { get; set; } = LeadSources.Google;

    [FirestoreProperty("serviceType")]
    public string ServiceType { get; set; } = string.Empty;

    [FirestoreProperty("location")]
    public string Location { get; set; } = string.Empty;

    /// <summary>Date of first contact, stored as a plain "yyyy-MM-dd" string (not a Timestamp).</summary>
    [FirestoreProperty("dateContact")]
    public string DateContact { get; set; } = string.Empty;

    [FirestoreProperty("status")]
    public string Status { get; set; } = LeadStatuses.New;

    [FirestoreProperty("notes")]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Set on every lead of one spreadsheet-import batch. Carried here because
    /// the notifyOnNewLead trigger keys off it — a lead written from this app
    /// without it will (correctly) alert the team.
    /// </summary>
    [FirestoreProperty("importBatchId")]
    public string? ImportBatchId { get; set; }

    [FirestoreProperty("clientId")]
    public string? ClientId { get; set; }
}

public class Job : FirestoreDocument
{
    [FirestoreProperty("jobName")]
    public string JobName { get; set; } = string.Empty;

    [FirestoreProperty("clientName")]
    public string ClientName { get; set; } = string.Empty;

    [FirestoreProperty("clientPhone")]
    public string ClientPhone { get; set; } = string.Empty;

    [FirestoreProperty("jobAddress")]
    public string JobAddress { get; set; } = string.Empty;

    [FirestoreProperty("serviceType")]
    public string ServiceType { get; set; } = string.Empty;

    [FirestoreProperty("status")]
    public string Status { get; set; } = JobStatuses.Scheduled;

    // All dates below are plain "yyyy-MM-dd" strings, and times "HH:mm" —
    // never Firestore Timestamps. The web Calendar derives its events from
    // these same fields.
    [FirestoreProperty("startDate")]
    public string StartDate { get; set; } = string.Empty;

    [FirestoreProperty("endDate")]
    public string EndDate { get; set; } = string.Empty;

    [FirestoreProperty("quoteDate")]
    public string QuoteDate { get; set; } = string.Empty;

    [FirestoreProperty("quoteTime")]
    public string QuoteTime { get; set; } = string.Empty;

    [FirestoreProperty("notes")]
    public string Notes { get; set; } = string.Empty;
}

public class TimeEntry : FirestoreDocument
{
    [FirestoreProperty("employeeId")]
    public string EmployeeId { get; set; } = string.Empty;

    [FirestoreProperty("employeeName")]
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>"yyyy-MM-dd".</summary>
    [FirestoreProperty("date")]
    public string Date { get; set; } = string.Empty;

    /// <summary>"HH:mm".</summary>
    [FirestoreProperty("startTime")]
    public string StartTime { get; set; } = string.Empty;

    [FirestoreProperty("endTime")]
    public string EndTime { get; set; } = string.Empty;

    /// <summary>Computed from start/end by whichever client wrote the entry.</summary>
    [FirestoreProperty("hours")]
    public double Hours { get; set; }

    [FirestoreProperty("notes")]
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// An Agenda task. Named AgendaTask rather than Task to avoid colliding with
/// System.Threading.Tasks.Task in every file that touches it.
/// </summary>
public class AgendaTask : FirestoreDocument
{
    [FirestoreProperty("title")]
    public string Title { get; set; } = string.Empty;

    [FirestoreProperty("notes")]
    public string Notes { get; set; } = string.Empty;

    [FirestoreProperty("status")]
    public string Status { get; set; } = TaskStatuses.Todo;

    /// <summary>Freeform and user-defined — deliberately not an enum.</summary>
    [FirestoreProperty("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>"yyyy-MM-dd", or empty when unset.</summary>
    [FirestoreProperty("dueDate")]
    public string DueDate { get; set; } = string.Empty;

    [FirestoreProperty("priority")]
    public string Priority { get; set; } = TaskPriorities.Medium;

    /// <summary>companies/{id}/employees id, empty when unassigned.</summary>
    [FirestoreProperty("assignedTo")]
    public string AssignedTo { get; set; } = string.Empty;
}

/// <summary>
/// A company's crew member. Historically free text — a name someone typed —
/// which is why Uid is optional: existing employee records have no login
/// attached, and dropping them would break the web Time page and the
/// QuickBooks payroll export (which matches on QbName).
///
/// Uid links the record to /users/{uid}. Only a linked employee can clock
/// themselves in, because that is the only case where the app can prove whose
/// shift it is.
/// </summary>
public class Employee : FirestoreDocument
{
    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty("role")]
    public string? Role { get; set; }

    /// <summary>Firebase Auth uid of the linked user, or null if unlinked.</summary>
    [FirestoreProperty("uid")]
    public string? Uid { get; set; }

    /// <summary>Denormalized from the linked user, so the roster reads without an extra lookup.</summary>
    [FirestoreProperty("email")]
    public string? Email { get; set; }

    /// <summary>Exact name in QuickBooks, for the web app's payroll export match.</summary>
    [FirestoreProperty("qbName")]
    public string? QbName { get; set; }

    public bool IsLinked => !string.IsNullOrWhiteSpace(Uid);
}

/// <summary>
/// Client-facing SEO Health Score — read-only, plain-language standing, NOT
/// the internal deep-audit CLI tool (that stays interactive and
/// internal-only). Written weekly by the computeSeoHealthScore Cloud
/// Function in the northstarapp repo; full scoring detail lives in that
/// function's source comment, not here — this app only ever displays it.
///
/// Lives at the TOP-LEVEL <c>seoHealth/{companyId}</c>, not nested under
/// <c>companies/{companyId}</c>, so a future web-portal screen reads the
/// exact same tenant-scoped document through the exact same Firestore rule
/// (manager-only) with zero backend changes — see FirestoreRepository.GetDocAsync.
/// </summary>
public class SeoHealth : FirestoreDocument
{
    /// <summary>0–100.</summary>
    [FirestoreProperty("score")]
    public int Score { get; set; }

    /// <summary>"Poor" / "Needs Work" / "Good" / "Excellent" — computed server-side, not derived here.</summary>
    [FirestoreProperty("scoreLabel")]
    public string ScoreLabel { get; set; } = string.Empty;

    [FirestoreProperty("lastChecked")]
    public DateTime? LastChecked { get; set; }

    /// <summary>Prior run's score, for the trend arrow. Null on the very first scan.</summary>
    [FirestoreProperty("previousScore")]
    public int? PreviousScore { get; set; }

    /// <summary>Top 2–3 only, highest-impact first — the Cloud Function does the ranking.</summary>
    [FirestoreProperty("topOpportunities")]
    public List<SeoOpportunity> TopOpportunities { get; set; } = [];
}

/// <summary>
/// One plain-language opportunity. No technical field names reach this
/// model — the Cloud Function has already translated everything.
/// </summary>
public class SeoOpportunity
{
    [FirestoreProperty("title")]
    public string Title { get; set; } = string.Empty;

    [FirestoreProperty("plainLanguageExplanation")]
    public string PlainLanguageExplanation { get; set; } = string.Empty;

    /// <summary>"high" / "medium" / "low" — see SeoImpacts.</summary>
    [FirestoreProperty("impact")]
    public string Impact { get; set; } = string.Empty;
}

public class Client : FirestoreDocument
{
    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty("phone")]
    public string Phone { get; set; } = string.Empty;

    [FirestoreProperty("email")]
    public string Email { get; set; } = string.Empty;
}
