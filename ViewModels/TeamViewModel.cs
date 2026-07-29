using System.Collections.ObjectModel;
using BigLocalHub.Models;
using BigLocalHub.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BigLocalHub.ViewModels;

/// <summary>
/// Manager-only crew roster: links each employee record to a real login.
///
/// This screen deliberately CANNOT create logins. Firebase Auth accounts are
/// created by a platform admin through the createCompanyUser Cloud Function —
/// a client has no way to mint an auth user, and relaxing that function so
/// managers could would mean managers setting other people's passwords. So the
/// picker here only offers users already assigned to this company.
///
/// The visibility check below is convenience only. The real boundary is
/// firestore.rules; see README for the manager rule that has to be deployed
/// before this is actually enforced.
/// </summary>
public partial class TeamViewModel : ObservableObject, IDisposable, Views.ILoadable
{
    private readonly SessionService _session;
    private readonly FirestoreRepository _repo;
    private IDisposable? _sub;
    private IReadOnlyList<Employee> _employees = [];
    private List<UserDoc> _companyUsers = [];
    private bool _loaded;

    public TeamViewModel(SessionService session, FirestoreRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    private string EmployeePath => $"companies/{_session.CompanyId}/employees";

    public ObservableCollection<TeamMemberRow> Members { get; } = [];
    public ObservableCollection<UserOption> UnlinkedUsers { get; } = [];

    [ObservableProperty] private bool _isManager;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _notice;
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string _summary = string.Empty;

    // ── Link sheet ──────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isLinkOpen;
    [ObservableProperty] private string _linkTargetName = string.Empty;
    [ObservableProperty] private bool _hasUnlinkedUsers;
    private string? _linkTargetId;

    public void Load()
    {
        if (_loaded) return;
        _loaded = true;

        IsManager = _session.IsManager;
        if (string.IsNullOrWhiteSpace(_session.CompanyId)) return;

        _sub = _repo.Watch<Employee>(EmployeePath, emps =>
        {
            _employees = emps;
            MainThread.BeginInvokeOnMainThread(Rebuild);
        }, ex => MainThread.BeginInvokeOnMainThread(() =>
        {
            Error = ex.Message.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase)
                ? "You don't have access to the team roster."
                : "Couldn't load the team. Check your connection.";
        }), orderByField: "name");

        _ = LoadCompanyUsersAsync();
    }

    /// <summary>
    /// Reads every /users doc for this company. Depends on the teammate-read
    /// rule in firestore.rules; if that hasn't been deployed the query fails
    /// with permission-denied, so the error says exactly that rather than
    /// showing an empty picker that looks like "no users exist".
    /// </summary>
    private async Task LoadCompanyUsersAsync()
    {
        try
        {
            var users = await _repo.QueryAsync<UserDoc>("users", "companyId", _session.CompanyId!);
            _companyUsers = users.ToList();
            MainThread.BeginInvokeOnMainThread(Rebuild);
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                Error = ex.Message.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase)
                    ? "Can't read the company's user list. The teammate-read Firestore rule may not be deployed yet."
                    : $"Couldn't load users: {ex.Message}");
        }
    }

    private void Rebuild()
    {
        Members.Clear();
        foreach (var e in _employees)
        {
            var linkedUser = _companyUsers.FirstOrDefault(u => u.Id == e.Uid);
            var linked = e.IsLinked;

            Members.Add(new TeamMemberRow(
                e.Id,
                e.Name,
                string.IsNullOrWhiteSpace(e.Role) ? "No role set" : e.Role!,
                linked
                    ? linkedUser?.Email ?? e.Email ?? "Linked"
                    : "No login linked — can't clock in",
                linked,
                linked ? "Unlink" : "Link login",
                StatusTones.Ink(linked ? StatusTone.Success : StatusTone.Warning),
                StatusTones.Tint(linked ? StatusTone.Success : StatusTone.Warning),
                linked ? "Linked" : "Not linked"));
        }

        var linkedCount = _employees.Count(e => e.IsLinked);
        Summary = $"{_employees.Count} on the crew · {linkedCount} can clock in";
    }

    [RelayCommand]
    private void OpenLink(string employeeId)
    {
        var emp = _employees.FirstOrDefault(e => e.Id == employeeId);
        if (emp is null) return;

        if (emp.IsLinked)
        {
            _ = UnlinkAsync(emp);
            return;
        }

        _linkTargetId = emp.Id;
        LinkTargetName = emp.Name;

        // Only offer users not already attached to another employee — one
        // login should map to exactly one crew member, or clock-in becomes
        // ambiguous.
        var taken = _employees.Where(e => e.IsLinked).Select(e => e.Uid!).ToHashSet();
        UnlinkedUsers.Clear();
        foreach (var u in _companyUsers.Where(u => !taken.Contains(u.Id)))
        {
            UnlinkedUsers.Add(new UserOption(
                u.Id,
                string.IsNullOrWhiteSpace(u.Name) ? u.Email : u.Name,
                u.Email,
                CompanyRoles.Label(u.CompanyRole)));
        }

        HasUnlinkedUsers = UnlinkedUsers.Count > 0;
        IsLinkOpen = true;
    }

    [RelayCommand]
    private void CloseLink() => IsLinkOpen = false;

    [RelayCommand]
    private async Task ChooseUserAsync(UserOption option)
    {
        if (Busy || _linkTargetId is null) return;

        Busy = true;
        Error = null;
        try
        {
            // Email is denormalized onto the employee so the roster renders
            // without a second read per row.
            await _repo.UpdateAsync(EmployeePath, _linkTargetId,
                ("uid", option.Uid),
                ("email", option.Email));

            Notice = $"{LinkTargetName} can now clock in.";
            IsLinkOpen = false;
        }
        catch (Exception ex)
        {
            Error = $"Couldn't link: {ex.Message}";
        }
        finally { Busy = false; }
    }

    private async Task UnlinkAsync(Employee emp)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is not null)
        {
            var ok = await page.DisplayAlertAsync(
                "Unlink login",
                $"{emp.Name} will no longer be able to clock in. Their logged hours are kept.",
                "Unlink", "Cancel");
            if (!ok) return;
        }

        Busy = true;
        try
        {
            // Hours stay: timeEntries reference employeeId, not uid, so
            // unlinking removes the ability to clock in without erasing history.
            await _repo.UpdateAsync(EmployeePath, emp.Id, ("uid", ""), ("email", ""));
            Notice = $"{emp.Name} unlinked.";
        }
        catch (Exception ex)
        {
            Error = $"Couldn't unlink: {ex.Message}";
        }
        finally { Busy = false; }
    }

    public void Dispose()
    {
        _sub?.Dispose();
        _sub = null;
        GC.SuppressFinalize(this);
    }
}

public record TeamMemberRow(
    string Id,
    string Name,
    string Role,
    string LinkDetail,
    bool IsLinked,
    string ActionLabel,
    Color StatusInk,
    Color StatusTint,
    string StatusLabel);

public record UserOption(string Uid, string DisplayName, string Email, string CompanyRoleLabel);
