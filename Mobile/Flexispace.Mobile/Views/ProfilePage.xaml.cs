using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;

    public ProfilePage(ProfileViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.AppearingCommand.Execute(null);
    }

    private async void OnMyBookingsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(MyBookingsPage));

    private async void OnLocationsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(LocationsPage));
}
