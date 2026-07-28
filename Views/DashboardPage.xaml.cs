using BigLocalHub.ViewModels;

namespace BigLocalHub.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _vm;
    private bool _loaded;

    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Guarded so returning to this tab doesn't stack a second set of
        // Firestore listeners on top of the ones already running.
        if (_loaded) return;
        _loaded = true;
        _vm.Load();
    }
}
