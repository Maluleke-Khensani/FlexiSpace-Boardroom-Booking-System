using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class UsersPage : ContentPage
{
    public UsersPage(UsersViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is UsersViewModel vm)
            vm.AppearingCommand.Execute(null);
    }
}
