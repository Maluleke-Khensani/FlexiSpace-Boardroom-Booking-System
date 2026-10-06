using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.Controls;

public partial class SocialLinksBar : ContentView
{
    public static readonly BindableProperty LightThemeProperty =
        BindableProperty.Create(nameof(LightTheme), typeof(bool), typeof(SocialLinksBar), true, propertyChanged: OnLightThemeChanged);

    public static readonly BindableProperty CaptionProperty =
        BindableProperty.Create(nameof(Caption), typeof(string), typeof(SocialLinksBar), "Follow Flexispace", propertyChanged: OnCaptionChanged);

    public SocialLinksBar()
    {
        InitializeComponent();
        ApplyTheme(LightTheme);
        Loaded += (_, _) => ApplyTheme(LightTheme);
    }

    /// <summary>True for ivory/light screens; false for dark Welcome hero.</summary>
    public bool LightTheme
    {
        get => (bool)GetValue(LightThemeProperty);
        set => SetValue(LightThemeProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    private static void OnLightThemeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SocialLinksBar bar)
            bar.ApplyTheme((bool)newValue);
    }

    private static void OnCaptionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SocialLinksBar bar)
            bar.CaptionLabel.Text = newValue as string ?? string.Empty;
    }

    private void ApplyTheme(bool light)
    {
        var res = Application.Current?.Resources;
        if (res is null) return;

        Color Ink(string key) => (Color)res[key];

        if (light)
        {
            CaptionLabel.TextColor = Ink("BrandMuted");
            StyleChip(FacebookChip, Ink("BrandSurface"), Ink("BrandLine"), Ink("BrandInk"));
            StyleChip(InstagramChip, Ink("BrandSurface"), Ink("BrandLine"), Ink("BrandInk"));
            StyleChip(LinkedInChip, Ink("BrandSurface"), Ink("BrandLine"), Ink("BrandInk"));
        }
        else
        {
            CaptionLabel.TextColor = Ink("BrandGoldSoft");
            var chipBg = Color.FromArgb("#22FFFFFF");
            var chipStroke = Ink("BrandGold");
            var chipText = Ink("BrandIvory");
            StyleChip(FacebookChip, chipBg, chipStroke, chipText);
            StyleChip(InstagramChip, chipBg, chipStroke, chipText);
            StyleChip(LinkedInChip, chipBg, chipStroke, chipText);
        }
    }

    private static void StyleChip(Border chip, Color background, Color stroke, Color text)
    {
        chip.BackgroundColor = background;
        chip.Stroke = stroke;
        if (chip.Content is Label label)
            label.TextColor = text;
    }

    private async void OnFacebookTapped(object? sender, TappedEventArgs e) =>
        await CompanyLinks.OpenAsync(CompanyLinks.Facebook);

    private async void OnInstagramTapped(object? sender, TappedEventArgs e) =>
        await CompanyLinks.OpenAsync(CompanyLinks.Instagram);

    private async void OnLinkedInTapped(object? sender, TappedEventArgs e) =>
        await CompanyLinks.OpenAsync(CompanyLinks.LinkedIn);
}
