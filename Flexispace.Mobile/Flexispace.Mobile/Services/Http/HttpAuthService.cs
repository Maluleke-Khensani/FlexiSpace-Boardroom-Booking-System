using System.Net.Http.Json;
using System.Text.Json;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services.Api;

namespace Flexispace.Mobile.Services.Http;

public sealed class HttpAuthService(
    ApiClient api,
    ITokenStorage tokens,
    CatalogSlugs catalog,
    IMsalAuthService msal) : IAuthService
{
    public User? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;
    public event EventHandler? AuthStateChanged;

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        try
        {
            using var response = await api.PostAsJsonAsync("api/DevAuth/token", new
            {
                email = email.Trim(),
                password
            });
            if (!response.IsSuccessStatusCode)
            {
                var err = await ReadErrorAsync(response);
                return new AuthResult
                {
                    Success = false,
                    Message = err ?? "Invalid email or password."
                };
            }

            var ok = await CompleteSignInAsync(response);
            return new AuthResult
            {
                Success = ok,
                Message = ok
                    ? $"Welcome, {CurrentUser?.Name ?? "operator"}."
                    : "Signed in, but the profile could not be loaded."
            };
        }
        catch
        {
            return new AuthResult
            {
                Success = false,
                Message = "Could not reach the API. Make sure FlexiSpace.API is running on http://localhost:5073."
            };
        }
    }

    public async Task<AuthResult> LoginWithMicrosoftAsync()
    {
        var msalResult = await msal.SignInInteractiveAsync();
        if (!msalResult.Success)
            return msalResult;

        try
        {
            // Access tokens often omit email — link oid to directory using MSAL account UPN first.
            ApiUserDto? me = null;
            if (!string.IsNullOrWhiteSpace(msalResult.LinkedEmailHint))
            {
                using var linkResponse = await api.PostAsJsonAsync("api/User/me/link", new
                {
                    email = msalResult.LinkedEmailHint
                });
                if (linkResponse.IsSuccessStatusCode)
                    me = await linkResponse.Content.ReadFromJsonAsync<ApiUserDto>(ApiClient.JsonOptions);
            }

            if (me is null)
            {
                using var meResponse = await api.SendAsync(new HttpRequestMessage(HttpMethod.Get, "api/User/me"));
                if (meResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    meResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    await tokens.ClearAsync();
                    var hint = meResponse.Headers.WwwAuthenticate.ToString();
                    if (string.IsNullOrWhiteSpace(hint))
                        hint = await meResponse.Content.ReadAsStringAsync();
                    var detail = string.IsNullOrWhiteSpace(hint) ? "" : $" Azure said: {hint.Trim()}";
                    return new AuthResult
                    {
                        Success = false,
                        Message = "Microsoft signed you in, but Azure rejected the token. On the App Service, set AzureAd__TenantId to 2427e95a-1238-4880-8236-996db36bc62c and AzureAd__ClientId / AzureAd__Audience to 77163347-59be-48f4-8675-535af30a3a53 (the API app, not the mobile app), then restart the App Service." + detail
                    };
                }

                if (meResponse.IsSuccessStatusCode)
                    me = await meResponse.Content.ReadFromJsonAsync<ApiUserDto>(ApiClient.JsonOptions);
            }

            if (me is null)
            {
                await tokens.ClearAsync();
                return new AuthResult
                {
                    Success = false,
                    Message = "Microsoft sign-in worked, but no FlexiSpace user is linked for this account. As Admin, open the Users tab and provision this email, then sign in again."
                };
            }

            await EnsureCatalogAsync();
            CurrentUser = MapUser(me);
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
            return new AuthResult
            {
                Success = true,
                Message = $"Welcome, {CurrentUser.Name}."
            };
        }
        catch (Exception ex)
        {
            await tokens.ClearAsync();
            return new AuthResult
            {
                Success = false,
                Message = $"Signed in with Microsoft, but the API rejected the profile call: {ex.Message}"
            };
        }
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        try
        {
            using var response = await api.PostAsJsonAsync("api/DevAuth/register", new
            {
                firstName = request.FirstName.Trim(),
                lastName = request.LastName.Trim(),
                email = request.Email.Trim(),
                phoneNumber = request.PhoneNumber.Trim(),
                password = request.Password
            });

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                return new AuthResult
                {
                    Success = false,
                    Message = "An account with this email already exists. Sign in instead."
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                var err = await ReadErrorAsync(response);
                return new AuthResult
                {
                    Success = false,
                    Message = err ?? "Could not create your account. Check the details and try again."
                };
            }

            var ok = await CompleteSignInAsync(response);
            return new AuthResult
            {
                Success = ok,
                Message = ok
                    ? "Account created. Welcome to Flexispace."
                    : "Account was created but sign-in failed. Try signing in."
            };
        }
        catch
        {
            return new AuthResult
            {
                Success = false,
                Message = "Could not reach the API. Make sure it is running."
            };
        }
    }

    public async Task LogoutAsync()
    {
        await tokens.ClearAsync();
        CurrentUser = null;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task SwitchDemoUserAsync(string email) => LoginAsync(email, "demo123");

    public IReadOnlyList<User> GetDemoUsers() => SeedData.Users;

    private async Task<bool> CompleteSignInAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<DevTokenResponse>(ApiClient.JsonOptions);
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
            return false;

        await tokens.SetAccessTokenAsync(payload.AccessToken);

        var me = await api.GetAsync<ApiUserDto>("api/User/me");
        if (me is null)
        {
            await tokens.ClearAsync();
            return false;
        }

        await EnsureCatalogAsync();
        CurrentUser = MapUser(me);
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private async Task EnsureCatalogAsync()
    {
        var locations = await api.GetAsync<List<ApiLocationDto>>("api/Location") ?? [];
        foreach (var loc in locations)
            catalog.RegisterLocation(loc.Id, loc.Name);

        var rooms = await api.GetAsync<List<ApiBoardroomDto>>("api/Boardroom") ?? [];
        foreach (var room in rooms)
            catalog.RegisterRoom(room.Id, room.Name, room.LocationId);
    }

    private User MapUser(ApiUserDto me)
    {
        var locationSlug = me.LocationId is int lid
            ? catalog.LocationSlugFromApi(lid)
            : null;

        return new User
        {
            Id = me.EntraObjectId == Guid.Empty ? IdAdapter.ToGuid(me.Id) : me.EntraObjectId,
            ApiId = me.Id,
            Name = $"{me.FirstName} {me.LastName}".Trim(),
            Email = me.Email,
            Role = ParseRole(me.Role),
            LocationId = locationSlug
        };
    }

    private static UserRole ParseRole(JsonElement role)
    {
        if (role.ValueKind == JsonValueKind.Number)
        {
            return role.GetInt32() switch
            {
                0 => UserRole.CentreManager,
                1 => UserRole.Staff,
                2 => UserRole.Administrator,
                3 => UserRole.Client,
                _ => UserRole.Staff
            };
        }

        if (role.ValueKind == JsonValueKind.String &&
            Enum.TryParse<UserRole>(role.GetString(), ignoreCase: true, out var parsed))
            return parsed;

        return UserRole.Staff;
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ApiErrorBody>(ApiClient.JsonOptions);
            return body?.Message;
        }
        catch
        {
            return null;
        }
    }
}
