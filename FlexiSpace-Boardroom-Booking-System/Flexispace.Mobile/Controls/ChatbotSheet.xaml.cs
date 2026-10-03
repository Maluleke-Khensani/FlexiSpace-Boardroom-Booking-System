namespace Flexispace.Mobile.Controls;

public partial class ChatbotSheet : ContentView
{
    public event EventHandler? CloseRequested;

    public ChatbotSheet()
    {
        InitializeComponent();
    }

    public async Task ScrollToEndAsync()
    {
        await Task.Delay(50);
        await TranscriptScroll.ScrollToAsync(0, double.MaxValue, true);
    }

    private void OnCloseClicked(object? sender, EventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnScrimTapped(object? sender, TappedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
