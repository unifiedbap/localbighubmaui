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

public class Employee : FirestoreDocument
{
    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty("role")]
    public string? Role { get; set; }
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
