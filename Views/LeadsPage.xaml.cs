using BigLocalHub.ViewModels;

namespace BigLocalHub.Views;

/// <summary>
/// Accepts ?status=… so the Dashboard's NEEDS ACTION rows can deep-link
/// straight into the matching filter.
/// </summary>
public partial class LeadsPage : ContentPage, IQueryAttributable
{
    private readonly LeadsViewModel _vm;

    public LeadsPage(LeadsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Load() guards itself, so repeated tab visits don't stack listeners.
        _vm.Load();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Shell keeps this page alive between tab switches, so the filter is
        // applied on every navigation rather than only on first construction.
        if (query.TryGetValue("status", out var raw) && raw is string status && !string.IsNullOrWhiteSpace(status))
        {
            _vm.Load();
            _vm.ApplyStatusFilter(Uri.UnescapeDataString(status));
        }
    }
}
