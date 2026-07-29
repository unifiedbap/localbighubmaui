namespace BigLocalHub.Services.Calendar;

/// <summary>
/// A calendar event in a form independent of any provider, so the Calendar
/// screen can push the same set to Apple, Google, or anything added later.
/// </summary>
/// <param name="SourceId">
/// Stable id derived from the originating Firestore doc (e.g. "job-abc123-start").
/// Written into the external event so a re-export updates the existing entry
/// instead of creating a duplicate every time someone taps Sync.
/// </param>
public record CalendarEventDto(
    string SourceId,
    string Title,
    DateTime Start,
    DateTime End,
    bool IsAllDay,
    string? Location,
    string? Notes);

public enum BridgeState
{
    /// <summary>Usable right now.</summary>
    Ready,
    /// <summary>Available but the user hasn't granted access yet.</summary>
    NeedsPermission,
    /// <summary>Needs credentials that aren't in the build (see GoogleCalendarConfig).</summary>
    NotConfigured,
    /// <summary>Not supported on this platform.</summary>
    Unsupported,
}

public record SyncResult(int Created, int Updated, int Failed, string? Message = null)
{
    public static SyncResult Error(string message) => new(0, 0, 0, message);
    public int Total => Created + Updated;
}

public interface ICalendarBridge
{
    /// <summary>Display name, e.g. "Apple Calendar".</summary>
    string Name { get; }

    /// <summary>Short explanation shown under the name in the UI.</summary>
    string Description { get; }

    Task<BridgeState> GetStateAsync();

    /// <summary>Prompts for whatever access this provider needs.</summary>
    Task<BridgeState> ConnectAsync();

    /// <summary>Pushes events out. Idempotent on SourceId.</summary>
    Task<SyncResult> ExportAsync(IReadOnlyList<CalendarEventDto> events);

    /// <summary>Forgets stored credentials/permissions where the provider allows it.</summary>
    Task DisconnectAsync();
}
