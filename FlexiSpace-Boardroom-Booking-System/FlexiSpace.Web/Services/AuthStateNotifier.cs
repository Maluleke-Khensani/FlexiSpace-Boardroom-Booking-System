using Flexispace.Core.Services;

namespace Flexispace.Web.Services;

//Bridges IAuthService auth events to Blazor UI refresh after login/logout/role switch.
public class AuthStateNotifier(IAuthService auth)
{
    public event Action? StateChanged;

    public void Initialize()
    {
        auth.AuthStateChanged += (_, _) => StateChanged?.Invoke();
    }
}
