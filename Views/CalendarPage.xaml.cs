using BigLocalHub.ViewModels;

namespace BigLocalHub.Views;

public partial class CalendarPage : ContentPage
{
    private readonly CalendarViewModel _vm;

    public CalendarPage(CalendarViewModel vm)
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
