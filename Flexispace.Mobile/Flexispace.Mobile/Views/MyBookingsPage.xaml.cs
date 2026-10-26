using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class MyBookingsPage : ContentPage
{
    private readonly MyBookingsViewModel _vm;

    public MyBookingsPage(MyBookingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.Watch(true);
        await _vm.AppearingCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        _vm.Watch(false);
        base.OnDisappearing();
    }
}
