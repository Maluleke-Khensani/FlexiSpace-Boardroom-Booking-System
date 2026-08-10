using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class LocationDetailPage : ContentPage
{
    public LocationDetailPage(LocationDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
