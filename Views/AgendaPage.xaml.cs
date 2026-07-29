using BigLocalHub.ViewModels;

namespace BigLocalHub.Views;

public partial class AgendaPage : ContentPage
{
    private readonly AgendaViewModel _vm;

    public AgendaPage(AgendaViewModel vm)
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
