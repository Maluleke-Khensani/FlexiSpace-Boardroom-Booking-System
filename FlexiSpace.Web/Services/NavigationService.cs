using Microsoft.AspNetCore.Components;

namespace Flexispace.Web.Services;

//Blazor implementation of INavigationService using NavigationManager.
public class NavigationService(NavigationManager navigation) : INavigationService
{
    public void NavigateTo(string uri, bool forceLoad = false) =>
        navigation.NavigateTo(uri, forceLoad);

    public Task ReloadAppAsync()
    {
        navigation.NavigateTo("/home", forceLoad: true);
        return Task.CompletedTask;
    }
}
