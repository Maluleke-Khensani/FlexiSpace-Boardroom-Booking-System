namespace Flexispace.Web.Services;

//Web navigation abstraction (replaces MAUI Shell). Used by ViewModels for page routing.
public interface INavigationService
{
    void NavigateTo(string uri, bool forceLoad = false);
    Task ReloadAppAsync();
}
