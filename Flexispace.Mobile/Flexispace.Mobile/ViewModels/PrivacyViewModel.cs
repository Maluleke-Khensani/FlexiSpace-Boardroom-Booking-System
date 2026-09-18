using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.ViewModels;

public partial class PrivacyViewModel : ObservableObject
{
    public string PageTitle => "Privacy policy";
    public string Eyebrow => "Protection of Personal Information";
    public string CompanyLine =>
        $"{PrivacyPolicyContent.CompanyName} · Reg. {PrivacyPolicyContent.RegistrationNumber}";
    public string Intro => PrivacyPolicyContent.Intro;
    public string Conclusion => PrivacyPolicyContent.Conclusion;
    public string ContactLine => $"Privacy requests · {PrivacyPolicyContent.ContactEmail}";
    public string SourceNote => "Same policy as published on flexispace.net.za";

    public IReadOnlyList<PrivacySection> Sections { get; } = PrivacyPolicyContent.Sections;

    [RelayCommand]
    private async Task OpenWebsitePolicyAsync()
    {
        try
        {
            await Browser.Default.OpenAsync(PrivacyPolicyContent.SourceUrl, BrowserLaunchMode.SystemPreferred);
        }
        catch
        {
            // Browser unavailable — policy is already shown in-app.
        }
    }

    [RelayCommand]
    private async Task OpenPopiaInfoAsync()
    {
        try
        {
            await Browser.Default.OpenAsync(PrivacyPolicyContent.PopiaInfoUrl, BrowserLaunchMode.SystemPreferred);
        }
        catch
        {
        }
    }

    [RelayCommand]
    private async Task ContactEmailAsync()
    {
        try
        {
            await Launcher.Default.OpenAsync($"mailto:{PrivacyPolicyContent.ContactEmail}?subject=Privacy%20request");
        }
        catch
        {
        }
    }
}
