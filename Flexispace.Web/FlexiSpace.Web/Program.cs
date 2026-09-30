using Flexispace.Core.Services;
using Flexispace.Web.Components;
using Flexispace.Web.Services;
using Flexispace.Web.Services.Real;
using Flexispace.Web.ViewModels;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

// Flexispace.Web entry point: registers Blazor Server, real Entra ID auth,
// the API-backed services, ViewModels, and routes.
//
// This replaces the previous Mock*-backed setup entirely - see
// FlexiSpace-Web-Wiring-README.md for what changed and why, and for the
// Azure App Registration steps this needs from Khensani before sign-in
// will actually work end to end.
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
    // token scoped to the FlexiSpace API (what BearerTokenHandler uses) -
    // without this, ITokenAcquisition has nothing to acquire against.
    .EnableTokenAcquisitionToCallDownstreamApi(new[] { apiOptions.Scope })
    .AddInMemoryTokenCaches();

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
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

// --- Real, API-backed services (replacing the Mock* registrations) ---
// Scoped, not Singleton: Blazor Server gives each circuit (each signed-in
// browser tab) its own DI scope, and every one of these reads the current
// HttpContext/user or calls the API on that specific person's behalf.
// Registering them Singleton - the way the Mock services were, since mock
// demo data is deliberately shared - would leak one user's session and
// CurrentUser into every other user's circuit.
builder.Services.AddHttpClient("FlexiSpaceApi", client =>
    {
        client.BaseAddress = new Uri(apiOptions.BaseUrl);
    })
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddScoped<FlexiSpaceApiClient>();

builder.Services.AddScoped<IAuthService, RealAuthService>();
builder.Services.AddScoped<IRoomService, RealRoomService>();
builder.Services.AddScoped<IBookingService, RealBookingService>();
builder.Services.AddScoped<IAdminService, RealAdminService>();
builder.Services.AddScoped<INotificationService, RealNotificationService>();

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
