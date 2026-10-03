using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class WelcomePage : ContentPage
{
    public WelcomePage(WelcomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
