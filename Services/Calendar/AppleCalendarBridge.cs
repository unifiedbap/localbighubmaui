#if IOS
using EventKit;
using Foundation;
#endif

namespace BigLocalHub.Services.Calendar;

/// <summary>
/// Writes hub events into the device's own calendar database via EventKit,
/// which is what "Apple Calendar" means on iOS — the system store the Calendar
/// app reads, including any iCloud/Exchange accounts the user has added. That
/// means no OAuth and no network: it works offline and needs no credentials.
///
/// Requires NSCalendarsUsageDescription and (iOS 17+)
/// NSCalendarsFullAccessUsageDescription in Info.plist. Without them iOS kills
/// the app on the first EventKit call rather than returning an error.
/// </summary>
public class AppleCalendarBridge : ICalendarBridge
{
    public string Name => "Apple Calendar";
    public string Description => "Writes into this device's calendar. No account needed.";

#if IOS
    private EKEventStore? _store;
    private EKEventStore Store => _store ??= new EKEventStore();

    public Task<BridgeState> GetStateAsync()
    {
        var status = EKEventStore.GetAuthorizationStatus(EKEntityType.Event);
        return Task.FromResult(status switch
        {
            EKAuthorizationStatus.Authorized => BridgeState.Ready,
            // iOS 17 splits authorization into full vs write-only. Write-only
            // is enough for export, which is all this bridge does.
            EKAuthorizationStatus.WriteOnly  => BridgeState.Ready,
            _                                => BridgeState.NeedsPermission,
        });
    }

    public async Task<BridgeState> ConnectAsync()
    {
        try
        {
            bool granted;
            if (OperatingSystem.IsIOSVersionAtLeast(17))
            {
                var result = await Store.RequestFullAccessToEventsAsync();
                granted = result.Item1;
            }
            else
            {
                var result = await Store.RequestAccessAsync(EKEntityType.Event);
                granted = result.Item1;
            }
            return granted ? BridgeState.Ready : BridgeState.NeedsPermission;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[apple-cal] access request failed: {ex}");
            return BridgeState.NeedsPermission;
        }
    }

    public async Task<SyncResult> ExportAsync(IReadOnlyList<CalendarEventDto> events)
    {
        if (await GetStateAsync() != BridgeState.Ready)
        {
            var state = await ConnectAsync();
            if (state != BridgeState.Ready)
                return SyncResult.Error("Calendar access was denied. Enable it in Settings › Privacy › Calendars.");
        }

        var target = Store.DefaultCalendarForNewEvents;
        if (target is null)
            return SyncResult.Error("No writable calendar is set up on this device.");

        int created = 0, updated = 0, failed = 0;

        foreach (var e in events)
        {
            try
            {
                // EventKit has no custom-field search, so the SourceId is
                // carried in the notes and matched over the event's own date
                // window. That keeps re-syncing idempotent without a local
                // mapping table that could drift from the real calendar.
                var existing = FindBySourceId(e.SourceId, e.Start.AddDays(-1), e.End.AddDays(1));
                var ev = existing ?? EKEvent.FromStore(Store);

                ev.Title     = e.Title;
                ev.StartDate = (NSDate)DateTime.SpecifyKind(e.Start, DateTimeKind.Local);
                ev.EndDate   = (NSDate)DateTime.SpecifyKind(e.End,   DateTimeKind.Local);
                ev.AllDay    = e.IsAllDay;
                ev.Location  = e.Location ?? string.Empty;
                ev.Notes     = BuildNotes(e);
                if (existing is null) ev.Calendar = target;

                if (Store.SaveEvent(ev, EKSpan.ThisEvent, true, out var error))
                {
                    if (existing is null) created++; else updated++;
                }
                else
                {
                    failed++;
                    System.Diagnostics.Debug.WriteLine($"[apple-cal] save failed: {error?.LocalizedDescription}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                System.Diagnostics.Debug.WriteLine($"[apple-cal] {e.SourceId}: {ex.Message}");
            }
        }

        return new SyncResult(created, updated, failed);
    }

    private EKEvent? FindBySourceId(string sourceId, DateTime from, DateTime to)
    {
        var predicate = Store.PredicateForEvents(
            (NSDate)DateTime.SpecifyKind(from, DateTimeKind.Local),
            (NSDate)DateTime.SpecifyKind(to,   DateTimeKind.Local),
            null);

        return Store.EventsMatching(predicate)?
            .FirstOrDefault(ev => ev.Notes?.Contains(Tag(sourceId), StringComparison.Ordinal) == true);
    }

    private static string Tag(string sourceId) => $"[biglocal:{sourceId}]";

    private static string BuildNotes(CalendarEventDto e) =>
        string.IsNullOrWhiteSpace(e.Notes)
            ? Tag(e.SourceId)
            : $"{e.Notes}\n\n{Tag(e.SourceId)}";

    public Task DisconnectAsync()
    {
        // EventKit permission is owned by iOS Settings; an app can't revoke it
        // itself, so this only drops the cached store handle.
        _store?.Dispose();
        _store = null;
        return Task.CompletedTask;
    }
#else
    public Task<BridgeState> GetStateAsync() => Task.FromResult(BridgeState.Unsupported);
    public Task<BridgeState> ConnectAsync()  => Task.FromResult(BridgeState.Unsupported);
    public Task<SyncResult> ExportAsync(IReadOnlyList<CalendarEventDto> events) =>
        Task.FromResult(SyncResult.Error("Apple Calendar is only available on iOS."));
    public Task DisconnectAsync() => Task.CompletedTask;
#endif
}
