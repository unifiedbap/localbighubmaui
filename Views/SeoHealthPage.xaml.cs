using BigLocalHub.ViewModels;

namespace BigLocalHub.Views;

public partial class SeoHealthPage : ContentPage
{
    private readonly SeoHealthViewModel _vm;

    public SeoHealthPage(SeoHealthViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Load() guards itself, so re-entering the page doesn't re-fetch needlessly.
        _vm.Load();
    }
}
