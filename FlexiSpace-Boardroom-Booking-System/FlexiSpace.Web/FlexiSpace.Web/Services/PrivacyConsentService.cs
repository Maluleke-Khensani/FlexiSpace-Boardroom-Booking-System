using Microsoft.JSInterop;

namespace Flexispace.Web.Services;

// Stores POPIA privacy-policy acceptance in the browser so it is required before sign-in.
public class PrivacyConsentService(IJSRuntime js)
{
    public const string StorageKey = "flexispace.privacyAccepted.v1";

    private bool? _accepted;

    public async Task<bool> HasAcceptedAsync()
    {
        if (_accepted.HasValue)
            return _accepted.Value;

        try
        {
            _accepted = await js.InvokeAsync<bool>("flexispacePrivacy.hasAccepted", StorageKey);
        }
        catch
        {
            _accepted = false;
        }

        return _accepted.Value;
    }

    public async Task AcceptAsync()
    {
        await js.InvokeVoidAsync("flexispacePrivacy.accept", StorageKey);
        _accepted = true;
    }
}
