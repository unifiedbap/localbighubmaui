using BigLocalHub.ViewModels;

namespace BigLocalHub.Views;

public partial class LeadsPage : ContentPage
{
    private readonly LeadsViewModel _vm;
    private bool _loaded;

    public LeadsPage(LeadsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;
        _vm.Load();
    }
}
