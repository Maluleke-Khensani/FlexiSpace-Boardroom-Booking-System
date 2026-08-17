using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class ManagePage : ContentPage
{
    private readonly ManageViewModel _vm;

    public ManagePage(ManageViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AppearingCommand.ExecuteAsync(null);
    }
}
