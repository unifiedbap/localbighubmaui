using BigLocalHub.Services;

namespace BigLocalHub.Views;

/// <summary>
/// Anything with Firestore listeners to start. Lets the host drive a module's
/// load without depending on OnAppearing, which doesn't fire for a page whose
/// content has been re-hosted here.
/// </summary>
public interface ILoadable
{
    void Load();
}

/// <summary>
/// Backs the shell's dynamic middle tab.
///
/// Rather than pushing module pages onto the Dashboard's navigation stack —
/// which left the tab bar highlighting "Dashboard" while the title said
/// "Calendar", with no way back — one persistent page swaps its own content.
/// The tab therefore always reflects where you actually are.
///
/// It hosts the module page's *content* rather than the page itself, because
/// Shell will not re-point a ShellContent at a different page reliably once
/// it has been shown. The module's view model comes across as the
/// BindingContext, so bindings (including RelativeSource lookups by view-model
/// type) resolve exactly as they did on the original page.
/// </summary>
public class ModuleHostPage : ContentPage
{
    private object? _currentViewModel;

    public string CurrentModuleKey { get; private set; } = string.Empty;

    public ModuleHostPage()
    {
        BackgroundColor = Tokens.Palette.PageBg;
    }

    public void ShowModule(ModuleInfo info, IServiceProvider services, IDictionary<string, object>? args = null)
    {
        if (info.PageType is null) return;

        // Re-selecting the module already on screen must not tear down and
        // rebuild it — that would drop scroll position and restart every
        // listener just for tapping the tab you're already on.
        if (CurrentModuleKey == info.Key && Content is not null)
        {
            if (args is not null && _currentPage is IQueryAttributable q) q.ApplyQueryAttributes(args);
            return;
        }

        // Releasing the outgoing view model matters: each one holds live
        // Firestore snapshot listeners, and swapping modules without disposing
        // would accumulate a listener per visit for the life of the session.
        if (_currentViewModel is IDisposable disposable) disposable.Dispose();

        var page = (ContentPage)services.GetRequiredService(info.PageType);
        var content = page.Content;
        // Detach first — a view can only have one parent, and leaving it
        // attached to the discarded page throws when we adopt it.
        page.Content = null;

        Title = info.Label;
        BindingContext = page.BindingContext;
        Content = content;

        _currentViewModel = page.BindingContext;
        _currentPage = page;
        CurrentModuleKey = info.Key;

        if (_currentViewModel is ILoadable loadable) loadable.Load();
        if (args is not null && page is IQueryAttributable queryable) queryable.ApplyQueryAttributes(args);
    }

    /// <summary>
    /// Kept only so query attributes can still be forwarded to the page object
    /// that owns the hosted content.
    /// </summary>
    private ContentPage? _currentPage;
}
