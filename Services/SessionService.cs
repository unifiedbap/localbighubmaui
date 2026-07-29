using System.ComponentModel;
using System.Runtime.CompilerServices;
using BigLocalHub.Models;
using Plugin.Firebase.Auth;
using Plugin.Firebase.Firestore;

namespace BigLocalHub.Services;

/// <summary>
/// Port of the AuthProvider in packages/core/auth.tsx — the single source of
/// truth for "who is signed in, at which company, with which modules".
///
/// Registered as a singleton and injected into every view model, the same way
/// the React app hangs one context off the tree.
/// </summary>
public class SessionService : INotifyPropertyChanged
{
    private readonly IFirebaseAuth _auth;
    private readonly IFirebaseFirestore _db;

    public SessionService(IFirebaseAuth auth, IFirebaseFirestore db)
    {
        _auth = auth;
        _db = db;
    }

    private IFirebaseUser? _firebaseUser;
    public IFirebaseUser? FirebaseUser
    {
        get => _firebaseUser;
        private set { _firebaseUser = value; Notify(); Notify(nameof(IsSignedIn)); }
    }

    private UserDoc? _userDoc;
    public UserDoc? UserDoc
    {
        get => _userDoc;
        private set
        {
            _userDoc = value;
            Notify();
            Notify(nameof(CompanyId));
            Notify(nameof(Role));
            Notify(nameof(IsAdmin));
        }
    }

    private Company? _company;
    public Company? Company
    {
        get => _company;
        private set { _company = value; Notify(); Notify(nameof(EnabledModules)); }
    }

    private bool _loading = true;
    public bool Loading
    {
        get => _loading;
        private set { _loading = value; Notify(); }
    }

    /// <summary>Set when the user doc or company doc could not be read at all.</summary>
    private string? _loadError;
    public string? LoadError
    {
        get => _loadError;
        private set { _loadError = value; Notify(); }
    }

    public string? CompanyId => UserDoc?.CompanyId;
    public string  Role      => UserDoc?.Role ?? UserRoles.User;
    public bool    IsAdmin   => UserDoc?.IsAdmin ?? false;
    public bool    IsSignedIn => FirebaseUser is not null;

    /// <summary>
    /// Runs this company's crew. Absent companyRole reads as staff, so nobody
    /// gains manager rights without an explicit grant.
    ///
    /// This gates UI only. The authoritative check has to live in
    /// firestore.rules — a client-side flag is a convenience, not a security
    /// boundary, and anything relying on it alone is bypassable.
    /// </summary>
    public bool IsManager => UserDoc?.IsManager ?? false;

    /// <summary>Firebase Auth uid of the signed-in user, or null.</summary>
    public string? Uid => FirebaseUser?.Uid;

    public IReadOnlyList<string> EnabledModules => Company?.EnabledModules ?? [];

    public bool HasModule(string module) => EnabledModules.Contains(module);

    /// <summary>Raised after every completed auth-state resolution, so the shell can rebuild its tabs.</summary>
    public event EventHandler? SessionChanged;

    /// <summary>
    /// Starts listening for auth changes. Called once at startup; the callback
    /// fires immediately with the restored session if one exists.
    /// </summary>
    public void Start()
    {
        _auth.AddAuthStateListener(async _ =>
        {
            try
            {
                await ResolveAsync(_auth.CurrentUser);
            }
            catch (Exception ex)
            {
                // Without this, a throw inside this async callback (permission
                // denied, offline with an empty cache, a malformed doc) leaves
                // Loading stuck true forever — an infinite spinner with nothing
                // on screen explaining why. Same failure the React version hit.
                System.Diagnostics.Debug.WriteLine($"[auth] failed resolving session: {ex}");
                UserDoc = null;
                Company = null;
                LoadError = ex.Message;
                Loading = false;
                SessionChanged?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private async Task ResolveAsync(IFirebaseUser? user)
    {
        FirebaseUser = user;

        if (user is null)
        {
            UserDoc = null;
            Company = null;
            LoadError = null;
            Loading = false;
            SessionChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        LoadError = null;

        var userSnap = await _db.GetDocument($"users/{user.Uid}").GetDocumentSnapshotAsync<UserDoc>();
        var uDoc = userSnap.Data;

        if (uDoc is null)
        {
            // Signed in but no /users doc yet — a brand new account mid-setup.
            UserDoc = null;
            Company = null;
            Loading = false;
            SessionChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        UserDoc = uDoc;

        if (!string.IsNullOrWhiteSpace(uDoc.CompanyId))
        {
            var compSnap = await _db.GetDocument($"companies/{uDoc.CompanyId}")
                                    .GetDocumentSnapshotAsync<Company>();
            Company = compSnap.Data;
        }
        else
        {
            // Platform admins belong to no company.
            Company = null;
        }

        Loading = false;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task SignInAsync(string email, string password) =>
        _auth.SignInWithEmailAndPasswordAsync(email.Trim(), password);

    public async Task SignOutAsync()
    {
        await _auth.SignOutAsync();
        UserDoc = null;
        Company = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
