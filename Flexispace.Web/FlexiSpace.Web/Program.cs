using Flexispace.Core.Services;
using Flexispace.Web.Components;
using Flexispace.Web.Services;
using Flexispace.Web.Services.Real;
using Flexispace.Web.ViewModels;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

// Flexispace.Web entry point: registers Blazor Server, real Entra ID auth,
// the API-backed services, ViewModels, and routes. Setup (user secrets,
// Azure app registration) is in Flexispace.Web/README.md.
var builder = WebApplication.CreateBuilder(args);

var apiOptions = builder.Configuration
    .GetSection(FlexiSpaceApiOptions.SectionName)
    .Get<FlexiSpaceApiOptions>() ?? new FlexiSpaceApiOptions();

builder.Services.Configure<FlexiSpaceApiOptions>(
    builder.Configuration.GetSection(FlexiSpaceApiOptions.SectionName));

// --- Authentication: Microsoft Entra ID sign-in (OpenID Connect) ---
// This is a confidential *web app* registration (as opposed to the API's
// own "web API" registration and the TestClient's "SPA" one) - same
// tenant/app registration as the API and TestClient, but needs its own
// "Web" platform + redirect URI + client secret added in Azure. See the
// README for the exact Azure Portal steps.
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    // Lets this app silently exchange the user's sign-in for an access
    // token scoped to the FlexiSpace API (what AccessTokenProvider uses) -
    // without this, ITokenAcquisition has nothing to acquire against.
    .EnableTokenAcquisitionToCallDownstreamApi(new[] { apiOptions.Scope })
    .AddInMemoryTokenCaches();

// The token cache above is in-memory, so it's wiped on every restart while
// the browser's sign-in cookie survives - leaving a "signed in but no
// token" state where every API call 401s. This rejects the cookie when its
// account is missing from the cache, forcing a clean re-sign-in. See
// RejectSessionCookieWhenAccountNotInCacheEvents for details.
builder.Services.Configure<CookieAuthenticationOptions>(
    CookieAuthenticationDefaults.AuthenticationScheme,
    options => options.Events = new RejectSessionCookieWhenAccountNotInCacheEvents(apiOptions.Scope));

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
// Flows the real signed-in ClaimsPrincipal into every Blazor circuit via
// a cascading AuthenticationState - this is what AccessTokenProvider
// reads (HttpContext isn't reliable inside an interactive circuit).
builder.Services.AddCascadingAuthenticationState();

// Microsoft.Identity.Web's sign-in/sign-out endpoints
// (/MicrosoftIdentity/Account/SignIn, /SignOut) are plain MVC controllers,
// not Razor components - Blazor's own Router can't issue the redirect
// challenge a Blazor circuit needs to leave, so these are mapped
// alongside the Razor components below.
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// --- API-backed services ---
// Scoped, not Singleton: Blazor Server gives each circuit (each signed-in
// browser tab) its own DI scope, and every one of these reads the current
// user or calls the API on that person's behalf. Singleton would leak one
// user's session into every other user's circuit.
builder.Services.AddHttpClient("FlexiSpaceApi", client =>
    {
        client.BaseAddress = new Uri(apiOptions.BaseUrl);
    });
// No DelegatingHandler for the token: IHttpClientFactory pools handlers
// outside the circuit's scope, so they can't see the signed-in user.
// FlexiSpaceApiClient attaches the token itself via AccessTokenProvider.
builder.Services.AddScoped<IAccessTokenProvider, AccessTokenProvider>();
builder.Services.AddScoped<FlexiSpaceApiClient>();

builder.Services.AddScoped<IAuthService, RealAuthService>();
builder.Services.AddScoped<IRoomService, RealRoomService>();
builder.Services.AddScoped<IBookingService, RealBookingService>();
builder.Services.AddScoped<IAdminService, RealAdminService>();
builder.Services.AddScoped<INotificationService, RealNotificationService>();
// Reports & audit page (/reports) - booking stats, CSV export, audit log.
builder.Services.AddScoped<ReportsApiService>();

builder.Services.AddScoped<INavigationService, NavigationService>();
builder.Services.AddScoped<PrivacyConsentService>();
// Was Singleton - now Scoped to match IAuthService above. MainLayout
// injects this directly per-circuit instead of it being resolved once
// from the root container (see the removed app.Services.GetRequiredService
// call that used to be here).
builder.Services.AddScoped<AuthStateNotifier>();

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
builder.Services.AddTransient<ReportsViewModel>();

var app = builder.Build();

// NOTE: the old `app.Services.GetRequiredService<AuthStateNotifier>().Initialize()`
// call that used to live here is gone - AuthStateNotifier is Scoped now,
// so it can't be resolved from the root container at startup. MainLayout
// resolves and initializes its own per-circuit instance instead.

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers(); // MicrosoftIdentity/Account/SignIn and /SignOut
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
