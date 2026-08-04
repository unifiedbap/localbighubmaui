using BigLocalHub.Services;
using BigLocalHub.Views;

namespace BigLocalHub;

/// <summary>
/// Root of the auth state machine, mirroring apps/web/src/App.tsx:
///
///   loading                 -> splash
///   signed out              -> LoginPage
///   signed in, no user doc  -> LoginPage with a message (account mid-setup)
///   signed in with company  -> AppShell, tabs gated by enabledModules
///
/// Every transition runs through here, so sign-in, sign-out, and a session
/// restored at cold start all take the same path.
/// </summary>
public partial class App : Application
{
    private readonly SessionService _session;
    private readonly IServiceProvider _services;
    private Window? _window;

    public App(SessionService session, IServiceProvider services)
    {
        InitializeComponent();
        _session = session;
        _services = services;

        // Light is the product theme, not merely the default-when-unset: the
        // hub is read outdoors in direct sun on job sites. Pinning it also
        // stops the OS dark-mode setting from half-applying to screens whose
        // tokens only define light values. A dark variant would mean adding
        // AppThemeBinding pairs across Tokens/Colors.xaml first.
        UserAppTheme = AppTheme.Light;

        _session.SessionChanged += (_, _) =>
            MainThread.BeginInvokeOnMainThread(ApplyRootPage);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _window = new Window(BuildRootPage());
#if IOS
        // Attempt #2 at the black-bar fix — attempt #1 (setting
        // UIApplication.SharedApplication.KeyWindow's background in
        // MauiProgram's FinishedLaunching hook) confirmed to do nothing,
        // meaning KeyWindow was null there (this app uses iOS's scene-based
        // lifecycle, where the window doesn't exist that early). This hooks
        // MAUI's own Window.HandlerChanged instead, which only fires once
        // the real native UIWindow has actually been created — no timing
        // guesswork about AppDelegate/scene lifecycle order.
        _window.HandlerChanged += OnWindowHandlerChanged;
#endif
        // Start listening only after the window exists, so the first callback
        // has somewhere to swap a page into.
        _session.Start();
        return _window;
    }

#if IOS
    /// <summary>
    /// DIAGNOSTIC — magenta instead of white on purpose, so it's obvious
    /// from a screenshot whether this is the view behind the reported black
    /// bar before committing to it as the real fix.
    /// </summary>
    private static void OnWindowHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is Window { Handler.PlatformView: UIKit.UIWindow uiWindow })
            uiWindow.BackgroundColor = UIKit.UIColor.Magenta;
    }
#endif

    private void ApplyRootPage()
    {
        if (_window is null) return;
        _window.Page = BuildRootPage();
    }

    private Page BuildRootPage()
    {
        if (_session.Loading)
            return new SplashPage();

        if (!_session.IsSignedIn)
            return _services.GetRequiredService<LoginPage>();

        // Signed in, but the /users doc hasn't been created yet (or couldn't be
        // read). Showing the shell here would render a company-less app with
        // empty tabs, so route back to login with the reason instead.
        if (_session.UserDoc is null)
            return new NoticePage(
                "Account not set up",
                _session.LoadError is not null
                    ? $"We signed you in, but couldn't load your profile.\n\n{_session.LoadError}"
                    : "This account isn't attached to a company yet. Ask your admin to finish setting it up.",
                async () => await _session.SignOutAsync());

        // Platform admins have no company — the admin console is web-only.
        if (_session.IsAdmin || string.IsNullOrWhiteSpace(_session.CompanyId))
            return new NoticePage(
                "Admin account",
                "This is a platform admin account. The admin console is web-only — sign in with a company account to use the mobile hub.",
                async () => await _session.SignOutAsync());

        return _services.GetRequiredService<AppShell>();
    }
}
