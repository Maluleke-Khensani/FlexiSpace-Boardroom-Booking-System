using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class BookingDetailPage : ContentPage
{
    public BookingDetailPage(BookingDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
