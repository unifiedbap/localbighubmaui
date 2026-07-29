using BigLocalHub.ViewModels;

namespace BigLocalHub.Views;

public partial class JobsPage : ContentPage
{
    private readonly JobsViewModel _vm;

    public JobsPage(JobsViewModel vm)
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
