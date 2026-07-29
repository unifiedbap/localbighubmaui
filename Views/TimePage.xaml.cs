using BigLocalHub.ViewModels;

namespace BigLocalHub.Views;

public partial class TimePage : ContentPage
{
    private readonly TimeViewModel _vm;

    public TimePage(TimeViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Load() guards itself, so re-entering the page doesn't stack listeners.
        _vm.Load();
    }
}
