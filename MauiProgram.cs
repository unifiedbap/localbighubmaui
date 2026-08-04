using BigLocalHub.Services;
using BigLocalHub.Services.Calendar;
using BigLocalHub.ViewModels;
using BigLocalHub.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Plugin.Firebase.Auth;
using Plugin.Firebase.Firestore;

#if IOS
using Plugin.Firebase.Core.Platforms.iOS;
#elif ANDROID
using Plugin.Firebase.Core.Platforms.Android;
#endif

namespace BigLocalHub;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .RegisterFirebaseServices()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ── App services ────────────────────────────────────────────────────
        builder.Services.AddSingleton<FirestoreRepository>();
        // Singleton: one session for the whole app, same as the single React
        // context provider at the root of the web and Expo trees.
        builder.Services.AddSingleton<SessionService>();
        builder.Services.AddSingleton<UserPreferences>();

        // ── Calendar bridges ────────────────────────────────────────────────
        // Registered as a collection so CalendarViewModel takes
        // IEnumerable<ICalendarBridge> and a third provider slots in without
        // touching the view model.
        builder.Services.AddSingleton<ICalendarBridge, AppleCalendarBridge>();
        builder.Services.AddSingleton<ICalendarBridge, GoogleCalendarBridge>();

        // ── View models ─────────────────────────────────────────────────────
        // Transient so a page rebuilt after sign-out gets clean state and fresh
        // Firestore listeners rather than ones bound to the previous company.
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<LeadsViewModel>();
        builder.Services.AddTransient<MoreViewModel>();
        builder.Services.AddTransient<JobsViewModel>();
        builder.Services.AddTransient<TimeViewModel>();
        builder.Services.AddTransient<AgendaViewModel>();
        builder.Services.AddTransient<CalendarViewModel>();
        builder.Services.AddTransient<TeamViewModel>();
        builder.Services.AddTransient<SeoHealthViewModel>();

        // ── Pages ───────────────────────────────────────────────────────────
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<LeadsPage>();
        builder.Services.AddTransient<MorePage>();
        builder.Services.AddTransient<JobsPage>();
        builder.Services.AddTransient<TimePage>();
        builder.Services.AddTransient<AgendaPage>();
        builder.Services.AddTransient<CalendarPage>();
        builder.Services.AddTransient<TeamPage>();
        builder.Services.AddTransient<SeoHealthPage>();
        builder.Services.AddTransient<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    /// <summary>
    /// Initializes the native Firebase SDK from the platform config file
    /// (GoogleService-Info.plist on iOS, google-services.json on Android) and
    /// registers the Auth/Firestore singletons for injection.
    /// </summary>
    private static MauiAppBuilder RegisterFirebaseServices(this MauiAppBuilder builder)
    {
        builder.ConfigureLifecycleEvents(events =>
        {
#if IOS
            events.AddiOS(iOS => iOS.WillFinishLaunching((_, __) =>
            {
                CrossFirebase.Initialize();
                return false;
            }));
#elif ANDROID
            events.AddAndroid(android => android.OnCreate((activity, _) =>
                CrossFirebase.Initialize(activity)));
#endif
        });

        builder.Services.AddSingleton(_ => CrossFirebaseAuth.Current);
        builder.Services.AddSingleton(_ => CrossFirebaseFirestore.Current);

        return builder;
    }
}
