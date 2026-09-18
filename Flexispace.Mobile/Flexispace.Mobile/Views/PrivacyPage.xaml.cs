using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class PrivacyPage : ContentPage
{
    public PrivacyPage(PrivacyViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
