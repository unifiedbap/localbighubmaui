using BigLocalHub.ViewModels;

namespace BigLocalHub.Views;

public partial class TeamPage : ContentPage
{
    private readonly TeamViewModel _vm;

    public TeamPage(TeamViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Load();
    }
}
