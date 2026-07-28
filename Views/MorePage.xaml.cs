using BigLocalHub.ViewModels;

namespace BigLocalHub.Views;

public partial class MorePage : ContentPage
{
    private readonly MoreViewModel _vm;

    public MorePage(MoreViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Cheap and synchronous — just re-reads the session, so no guard needed.
        _vm.Refresh();
    }
}
