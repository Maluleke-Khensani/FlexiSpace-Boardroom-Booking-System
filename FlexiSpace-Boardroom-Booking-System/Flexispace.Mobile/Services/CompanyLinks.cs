namespace Flexispace.Mobile.Services;

/// <summary>
/// Official Flexispace social profiles from https://flexispace.net.za/ footer.
/// </summary>
public static class CompanyLinks
{
    public const string Website = "https://flexispace.net.za/";
    public const string Facebook = "https://www.facebook.com/Flexi-Work-Space-397763787484189/";
    public const string Instagram = "https://www.instagram.com/flexiworkspace8/?hl=en";
    public const string LinkedIn = "https://www.linkedin.com/company/flexi-work-space/";

    public static async Task OpenAsync(string url)
    {
        try
        {
            await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
        }
        catch
        {
            if (Shell.Current is not null)
                await Shell.Current.DisplayAlertAsync("Could not open link", url, "OK");
        }
    }
}
