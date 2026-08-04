using BigLocalHub.Models;

namespace BigLocalHub.Services;

/// <summary>
/// Per-user UI preferences — currently which six modules appear as the
/// Dashboard's Quick Actions.
///
/// Stored in device-local <see cref="Preferences"/> rather than on
/// /users/{uid}, and that is a deliberate constraint, not an oversight: the
/// Firestore rule for that document only permits a teammate to write
/// notifyPrefs, phone, fcmTokens, and updatedAt. Adding a quickActions field
/// would be rejected with permission-denied, and shipping a write that always
/// fails is worse than storing locally.
///
/// The trade-off is that the choice doesn't follow the user to another device.
/// To make it sync, add 'quickActions' to touchesOnlyNotifyFields() in
/// firestore.rules, deploy, then swap the two methods below for a Firestore
/// read/write — nothing else has to change.
///
/// Keys are namespaced by uid so two accounts on one device (an owner and an
/// office admin sharing a tablet) don't inherit each other's layout.
/// </summary>
public class UserPreferences
{
    private const string QuickActionsKeyPrefix = "quick_actions_v1_";

    /// <summary>
    /// Defaults, in order: the four things someone opens the app to do that
    /// aren't already a bottom tab.
    /// </summary>
    public static readonly string[] DefaultQuickActions =
        [Modules.Jobs, Modules.Time, Modules.Calendar, Modules.Agenda];

    /// <summary>
    /// Six slots, only four defaults above — GetQuickActions tops the
    /// remainder up from whatever else the company has enabled, so the extra
    /// slots aren't blank on a first launch, just not opinionated about which
    /// two modules fill them.
    /// </summary>
    public const int SlotCount = 6;

    private static string KeyFor(string uid) => QuickActionsKeyPrefix + uid;

    /// <summary>
    /// The user's chosen modules, filtered to what their company actually has
    /// enabled and this client implements, then topped up from the defaults so
    /// the grid is never short. A company without, say, Time still gets six
    /// usable tiles.
    /// </summary>
    public IReadOnlyList<string> GetQuickActions(string uid, IEnumerable<string> enabledModules)
    {
        var allowed = ModuleRegistry.AvailableFor(enabledModules).Select(m => m.Key).ToList();

        var stored = Preferences.Get(KeyFor(uid), string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(allowed.Contains)
            .Distinct()
            .ToList();

        foreach (var fallback in DefaultQuickActions.Concat(allowed))
        {
            if (stored.Count >= SlotCount) break;
            if (allowed.Contains(fallback) && !stored.Contains(fallback)) stored.Add(fallback);
        }

        return stored.Take(SlotCount).ToList();
    }

    public void SetQuickActions(string uid, IEnumerable<string> modules) =>
        Preferences.Set(KeyFor(uid), string.Join(',', modules.Take(SlotCount)));

    public void ResetQuickActions(string uid) => Preferences.Remove(KeyFor(uid));
}
