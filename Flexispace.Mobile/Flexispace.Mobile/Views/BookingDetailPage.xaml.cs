using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class BookingDetailPage : ContentPage
{
    public BookingDetailPage(BookingDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    /// <summary>
    /// WinUI DatePicker/TimePicker/Entry often keep the typed value in the control
    /// until blur. Push those values into the VM immediately before save.
    /// </summary>
    private async void OnSaveChangesClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not BookingDetailViewModel vm) return;

        vm.ApplyScheduleDraft(
            EditDatePicker.Date ?? vm.EditDate,
            EditStartPicker.Time ?? vm.EditStart,
            EditEndPicker.Time ?? vm.EditEnd,
            AttendeesEntry.Text ?? string.Empty,
            NotesEditor.Text ?? string.Empty);

        await vm.SaveEditsCommand.ExecuteAsync(null);
        SyncEditorsFromViewModel(vm);
    }

    private async void OnSaveStatusClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not BookingDetailViewModel vm) return;
        if (StatusPicker.SelectedItem is Models.BookingStatus status)
            vm.SelectedStatus = status;
        await vm.SaveStatusCommand.ExecuteAsync(null);
        SyncEditorsFromViewModel(vm);
    }

    private async void OnCancelEditsClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not BookingDetailViewModel vm) return;
        await vm.CancelEditsCommand.ExecuteAsync(null);
        SyncEditorsFromViewModel(vm);
    }

    private void SyncEditorsFromViewModel(BookingDetailViewModel vm)
    {
        EditDatePicker.Date = vm.EditDate;
        EditStartPicker.Time = vm.EditStart;
        EditEndPicker.Time = vm.EditEnd;
        AttendeesEntry.Text = vm.AttendeesText;
        NotesEditor.Text = vm.Notes;
        StatusPicker.SelectedItem = vm.SelectedStatus;
    }
}
