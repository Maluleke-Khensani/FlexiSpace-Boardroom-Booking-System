using System.Net.Http.Headers;
using System.Net.Http.Json;
using Flexispace.Core.Services;
using Flexispace.Web.Api;
using Flexispace.Web.Components;
using Flexispace.Web.Services;
using Flexispace.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddMicrosoftIdentityConsentHandler();
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

var initialScopes = builder.Configuration.GetSection("FlexiSpaceApi:Scopes").Get<string[]>()
    ?? builder.Configuration["FlexiSpaceApi:Scopes"]?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
    ?? [];

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi(initialScopes)
    .AddInMemoryTokenCaches();

builder.Services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.SaveTokens = true;
    var previousRedirect = options.Events.OnRedirectToIdentityProvider;
    options.Events.OnRedirectToIdentityProvider = async context =>
    {
        if (previousRedirect is not null)
            await previousRedirect(context);

        context.ProtocolMessage.Prompt = "select_account";
    };

    var previous = options.Events.OnTokenValidated;
    options.Events.OnTokenValidated = async context =>
    {
        if (previous is not null)
            await previous(context);

        var scopes = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()
            .GetSection("FlexiSpaceApi:Scopes")
            .Get<string[]>()
            ?? [];
        var cache = context.HttpContext.RequestServices.GetRequiredService<AccessTokenCache>();
        var key = AccessTokenCache.UserKey(context.Principal);
        var token = context.TokenEndpointResponse?.AccessToken;
        if (string.IsNullOrWhiteSpace(token) && scopes.Length > 0 && context.Principal is not null)
        {
            try
            {
                token = await context.HttpContext.RequestServices
                    .GetRequiredService<ITokenAcquisition>()
                    .GetAccessTokenForUserAsync(scopes, user: context.Principal);
            }
            catch
            {
                // The /auth/complete endpoint will try again with HttpContext.
            }
        }

        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(token))
            cache.Set(key, token);
    };
});

builder.Services.AddAuthorization();
builder.Services.AddSingleton<AccessTokenCache>();

builder.Services.AddHttpClient("FlexiSpaceApi", (sp, client) =>
{
    var baseUrl = sp.GetRequiredService<IConfiguration>()["FlexiSpaceApi:BaseUrl"]
        ?? "https://flexispace-exdye9dzg3bhejh9.southafricanorth-01.azurewebsites.net";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<FlexiSpaceApiClient>();
builder.Services.AddScoped<IAuthService, ApiAuthService>();
builder.Services.AddScoped<ApiRoomService>();
builder.Services.AddScoped<IRoomService>(sp => sp.GetRequiredService<ApiRoomService>());
builder.Services.AddScoped<IBookingService, ApiBookingService>();
builder.Services.AddScoped<INotificationService, ApiNotificationService>();
builder.Services.AddScoped<IAdminService, ApiAdminService>();
builder.Services.AddScoped<BookingSessionCache>();
builder.Services.AddScoped<INavigationService, NavigationService>();
builder.Services.AddScoped<PrivacyConsentService>();

builder.Services.AddTransient<WelcomeViewModel>();
builder.Services.AddTransient<LoginViewModel>();
builder.Services.AddTransient<HomeViewModel>();
builder.Services.AddTransient<AvailabilityViewModel>();
builder.Services.AddTransient<BookingViewModel>();
builder.Services.AddTransient<MyBookingsViewModel>();
builder.Services.AddTransient<BookingDetailViewModel>();
builder.Services.AddTransient<BookingConfirmationViewModel>();
builder.Services.AddTransient<NotificationsViewModel>();
builder.Services.AddTransient<ProfileViewModel>();
builder.Services.AddTransient<LocationDetailViewModel>();
builder.Services.AddTransient<LocationsViewModel>();
builder.Services.AddTransient<ManageViewModel>();
builder.Services.AddTransient<UsersViewModel>();
builder.Services.AddTransient<ReportsViewModel>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/signin-microsoft", async (HttpContext context) =>
{
    var redirect = context.Request.Query["redirectUri"].FirstOrDefault() ?? "/home";
    if (!redirect.StartsWith('/') || redirect.StartsWith("//"))
        redirect = "/home";

    await context.ChallengeAsync(
        OpenIdConnectDefaults.AuthenticationScheme,
        new AuthenticationProperties { RedirectUri = redirect });
}).AllowAnonymous().DisableAntiforgery();

app.MapGet("/auth/complete", async (
    HttpContext http,
    ITokenAcquisition tokenAcquisition,
    AccessTokenCache tokenCache,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) =>
{
    if (http.User.Identity?.IsAuthenticated != true)
        return Results.Redirect("/login");

    var scopes = configuration.GetSection("FlexiSpaceApi:Scopes").Get<string[]>() ?? [];
    var key = AccessTokenCache.UserKey(http.User);
    var token = tokenCache.Get(key);
    if (string.IsNullOrWhiteSpace(token) && scopes.Length > 0)
    {
        try
        {
            token = await tokenAcquisition.GetAccessTokenForUserAsync(scopes, user: http.User);
        }
        catch
        {
            token = await http.GetTokenAsync("access_token");
        }
    }

    token ??= await http.GetTokenAsync("access_token");
    if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(token))
        tokenCache.Set(key, token);

    if (!string.IsNullOrWhiteSpace(token))
    {
        var client = httpClientFactory.CreateClient("FlexiSpaceApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/user/register")
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            await client.SendAsync(request);
        }
        catch
        {
            // Home/EnsureSession will surface the error if the API is down.
        }
    }

    return Results.Redirect("/home");
}).AllowAnonymous().DisableAntiforgery();

app.MapGet("/signout-microsoft", () => Results.SignOut(
    new AuthenticationProperties { RedirectUri = "/" },
    [
        CookieAuthenticationDefaults.AuthenticationScheme,
        OpenIdConnectDefaults.AuthenticationScheme
    ])).AllowAnonymous().DisableAntiforgery();

app.MapControllers();
app.MapRazorPages();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
