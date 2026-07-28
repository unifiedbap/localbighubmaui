using BigLocalHub.Models;

namespace BigLocalHub.Services;

/// <summary>
/// Port of packages/core/stageLabels.ts.
///
/// The Leads and Jobs modules were written for contractors, so their stored
/// status values say "Quote scheduled" / "Quoted". Those strings are the
/// canonical persisted values and they NEVER change — a consulting firm's
/// Firestore docs still say 'Quoted'. What changes is only what the UI prints.
///
/// Resolution order for any one status, first hit wins:
///   1. company.leadStatusLabels / jobStatusLabels  (per-company override)
///   2. the preset for company.businessType
///   3. the canonical value itself
///
/// Keep this free of Firebase types — it is pure data + pure functions, so it
/// can be unit tested and reused anywhere.
/// </summary>
public static class StageLabels
{
    public const string StyleQuote   = "quote";
    public const string StyleMeeting = "meeting";
    public const string StyleBooking = "booking";

    public static readonly IReadOnlyDictionary<string, string> PipelineStyleLabels =
        new Dictionary<string, string>
        {
            [StyleQuote]   = "Quote-based (contractor)",
            [StyleMeeting] = "Meeting-based (consulting / agency)",
            [StyleBooking] = "Booking-based (appointments / events)",
        };

    /// <summary>
    /// Only statuses that actually need rewording are listed; anything omitted
    /// renders as its canonical value ('New', 'Contacted', 'Won', 'Lost' are
    /// universal, so they never appear here).
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, string>> LeadPresets = new()
    {
        [StyleQuote] = new(),
        [StyleMeeting] = new()
        {
            [LeadStatuses.QuoteScheduled] = "Meeting scheduled",
            [LeadStatuses.Quoted]         = "Met",
        },
        [StyleBooking] = new()
        {
            [LeadStatuses.QuoteScheduled] = "Consultation booked",
            [LeadStatuses.Quoted]         = "Estimate given",
        },
    };

    private static readonly Dictionary<string, Dictionary<string, string>> JobPresets = new()
    {
        [StyleQuote] = new(),
        [StyleMeeting] = new()
        {
            [JobStatuses.QuoteScheduled] = "Meeting scheduled",
            [JobStatuses.Quoted]         = "Proposal sent",
            [JobStatuses.Scheduled]      = "Kickoff scheduled",
            [JobStatuses.Complete]       = "Delivered",
        },
        [StyleBooking] = new()
        {
            [JobStatuses.QuoteScheduled] = "Consultation booked",
            [JobStatuses.Quoted]         = "Estimate given",
            [JobStatuses.Scheduled]      = "Booked",
        },
    };

    private static readonly Dictionary<string, string> BusinessTypePipelineStyle = new()
    {
        ["general"]          = StyleQuote,
        ["agency"]           = StyleMeeting,
        ["consulting"]       = StyleMeeting,
        ["electrician"]      = StyleQuote,
        ["catering"]         = StyleBooking,
        ["landscaping"]      = StyleQuote,
        ["plumbing"]         = StyleQuote,
        ["hvac"]             = StyleQuote,
        ["laser-cleaning"]   = StyleQuote,
        ["excavation"]       = StyleQuote,
        ["photography"]      = StyleBooking,
        ["home-services"]    = StyleQuote,
        ["salon"]            = StyleBooking,
        ["barber"]           = StyleBooking,
        ["pet-grooming"]     = StyleBooking,
        ["dj"]               = StyleBooking,
        ["handyman"]         = StyleQuote,
        ["appliance-repair"] = StyleQuote,
        ["cleaning"]         = StyleBooking,
        ["home-builder"]     = StyleQuote,
        ["painting"]         = StyleQuote,
        ["auto-repair"]      = StyleQuote,
        ["lawn-care"]        = StyleBooking,
        ["carpentry"]        = StyleQuote,
        ["events"]           = StyleBooking,
    };

    public static string PipelineStyleFor(Company? company)
    {
        var bt = string.IsNullOrWhiteSpace(company?.BusinessType) ? "general" : company!.BusinessType!;
        return BusinessTypePipelineStyle.TryGetValue(bt, out var style) ? style : StyleQuote;
    }

    /// <summary>Fully-resolved label for every lead status, in canonical order.</summary>
    public static Dictionary<string, string> LeadStatusLabels(Company? company)
        => Resolve(LeadStatuses.All, LeadPresets[PipelineStyleFor(company)], company?.LeadStatusLabels);

    /// <summary>Fully-resolved label for every job status, in canonical order.</summary>
    public static Dictionary<string, string> JobStatusLabels(Company? company)
        => Resolve(JobStatuses.All, JobPresets[PipelineStyleFor(company)], company?.JobStatusLabels);

    public static string LeadStatusLabel(Company? company, string status)
        => LeadStatusLabels(company).TryGetValue(status, out var l) ? l : status;

    public static string JobStatusLabel(Company? company, string status)
        => JobStatusLabels(company).TryGetValue(status, out var l) ? l : status;

    private static Dictionary<string, string> Resolve(
        string[] canonical,
        Dictionary<string, string> preset,
        Dictionary<string, string>? custom)
    {
        var result = new Dictionary<string, string>();
        foreach (var s in canonical)
        {
            // An override that is present but blank means "fall back", matching
            // the admin editor, which stores nothing for an emptied box.
            if (custom is not null && custom.TryGetValue(s, out var c) && !string.IsNullOrWhiteSpace(c))
                result[s] = c.Trim();
            else if (preset.TryGetValue(s, out var p))
                result[s] = p;
            else
                result[s] = s;
        }
        return result;
    }
}
