using System.Net.Http.Json;
using Flexispace.Core.Models;
using Flexispace.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Flexispace.Web.Api;

public sealed class ApiAuthService(
    FlexiSpaceApiClient api,
    AuthenticationStateProvider authenticationStateProvider,
    NavigationManager navigation) : IAuthService
{
    public User? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;
    public bool IsSessionReady { get; private set; }
    public string? SessionError { get; private set; }
    public event EventHandler? AuthStateChanged;

    public async Task EnsureSessionAsync()
    {
        SessionError = null;
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            CurrentUser = null;
            IsSessionReady = true;
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (CurrentUser is not null)
        {
            IsSessionReady = true;
            return;
        }

        var registered = await api.PostAsJsonAsync("api/user/register", new { });
        UserDto? dto = null;
        if (registered.IsSuccessStatusCode)
        {
            dto = await registered.Content.ReadFromJsonAsync<UserDto>(FlexiSpaceApiClient.Json);
        }
        else
        {
            SessionError = await FlexiSpaceApiClient.ReadErrorAsync(registered);
        }

        dto ??= await api.GetAsync<UserDto>("api/user/me");
        if (dto is null)
        {
            SessionError ??= "Microsoft signed you in, but FlexiSpace could not load your profile. Start the API and try again.";
            IsSessionReady = true;
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        CurrentUser = CatalogMapper.ToUser(dto);
        SessionError = null;
        IsSessionReady = true;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task MicrosoftSignInAsync()
    {
        navigation.NavigateTo("signin-microsoft?redirectUri=/auth/complete", forceLoad: true);
        return Task.CompletedTask;
    }

    public Task<bool> LoginAsync(string email, string password)
    {
        _ = email;
        _ = password;
        return Task.FromResult(false);
    }

    public Task LogoutAsync()
    {
        CurrentUser = null;
        IsSessionReady = true;
        SessionError = null;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
        navigation.NavigateTo("signout-microsoft", forceLoad: true);
        return Task.CompletedTask;
    }

    public Task SwitchDemoUserAsync(string email)
    {
        _ = email;
        return Task.CompletedTask;
    }

    public IReadOnlyList<User> GetDemoUsers() => [];
}
