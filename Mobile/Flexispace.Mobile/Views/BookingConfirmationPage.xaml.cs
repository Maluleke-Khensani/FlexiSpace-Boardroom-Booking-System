using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class BookingConfirmationPage : ContentPage
{
    public BookingConfirmationPage(BookingConfirmationViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
