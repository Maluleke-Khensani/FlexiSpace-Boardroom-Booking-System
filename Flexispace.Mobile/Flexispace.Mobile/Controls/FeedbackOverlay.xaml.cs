using Microsoft.Maui.Controls.Shapes;

namespace Flexispace.Mobile.Controls;

public enum FeedbackTone
{
    Fail,
    Success,
    Info,
    Confirm
}

public sealed class FeedbackPrompt
{
    public string Title { get; init; } = "Please check";
    public string Message { get; init; } = string.Empty;
    public FeedbackTone Tone { get; init; } = FeedbackTone.Fail;
    public string AcceptText { get; init; } = "Got it";
    public string? CancelText { get; init; }
    public bool Destructive { get; init; }
}

public partial class FeedbackOverlay : ContentView
{
    private readonly TaskCompletionSource<bool> _done = new();
    private bool _closing;
    private bool _hasCancel;

    public FeedbackOverlay()
    {
        InitializeComponent();
    }

    public async Task<bool> PresentAsync(FeedbackPrompt prompt)
    {
        Apply(prompt);
        Opacity = 0;
        Card.Scale = 0.96;
        _ = this.FadeToAsync(1, 160, Easing.CubicOut);
        _ = Card.ScaleToAsync(1, 180, Easing.CubicOut);
        return await _done.Task;
    }

    private void Apply(FeedbackPrompt prompt)
    {
        TitleLabel.Text = prompt.Title;
        AcceptButton.Text = prompt.AcceptText;
        _hasCancel = !string.IsNullOrWhiteSpace(prompt.CancelText);
        CancelButton.IsVisible = _hasCancel;
        CancelButton.Text = prompt.CancelText ?? "Cancel";
        ApplyTone(prompt.Tone);

        if (prompt.Destructive)
        {
            AcceptButton.BackgroundColor = ColorOf("BrandDanger");
            AcceptButton.TextColor = Colors.White;
        }

        SplitSuggestions(prompt.Message, out var body, out var times);
        MessageLabel.Text = body;
        MessageLabel.IsVisible = body.Length > 0;
        FillSuggestions(times);
    }

    private void ApplyTone(FeedbackTone tone)
    {
        switch (tone)
        {
            case FeedbackTone.Success:
                Paint("BrandSuccessSoft", "BrandSuccess", "\ue86c", "BrandSuccess");
                break;
            case FeedbackTone.Info:
                Paint("BrandGoldWash", "BrandGoldDeep", "\ue88e", "BrandGold");
                break;
            case FeedbackTone.Confirm:
                Paint("BrandGoldWash", "BrandGoldDeep", "\ue002", "BrandGold");
                break;
            default:
                Paint("BrandDangerSoft", "BrandDanger", "\ue000", "BrandDanger");
                break;
        }
    }

    private void Paint(string wellKey, string glyphKey, string glyph, string strokeKey)
    {
        IconWell.BackgroundColor = ColorOf(wellKey);
        IconGlyph.TextColor = ColorOf(glyphKey);
        IconGlyph.Text = glyph;
        Card.Stroke = ColorOf(strokeKey);
    }

    private void FillSuggestions(IReadOnlyList<string> times)
    {
        Suggestions.Children.Clear();
        var show = times.Count > 0;
        SuggestionCaption.IsVisible = show;
        Suggestions.IsVisible = show;
        if (!show)
            return;

        var ink = ColorOf("BrandGoldDeep");
        var wash = ColorOf("BrandGoldWash");
        var stroke = ColorOf("BrandGold");
        foreach (var time in times)
        {
            Suggestions.Add(new Border
            {
                BackgroundColor = wash,
                Stroke = stroke,
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
                Padding = new Thickness(12, 6),
                Margin = new Thickness(4),
                Content = new Label
                {
                    Text = time,
                    TextColor = ink,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 13
                }
            });
        }
    }

    private static void SplitSuggestions(string message, out string body, out IReadOnlyList<string> times)
    {
        const string marker = "Try:";
        var index = message.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            body = message.Trim();
            times = [];
            return;
        }

        body = message[..index].Trim();
        times = message[(index + marker.Length)..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private void OnAcceptClicked(object? sender, EventArgs e) => Close(true);

    private void OnCancelClicked(object? sender, EventArgs e) => Close(false);

    private void OnScrimTapped(object? sender, TappedEventArgs e) => Close(!_hasCancel);

    private async void Close(bool accepted)
    {
        if (_closing)
            return;
        _closing = true;
        await Task.WhenAll(this.FadeToAsync(0, 120), Card.ScaleToAsync(0.98, 120));
        _done.TrySetResult(accepted);
    }

    private static Color ColorOf(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Black;
}
