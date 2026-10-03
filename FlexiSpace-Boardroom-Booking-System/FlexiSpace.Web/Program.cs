using Flexispace.Core.Services;
using Flexispace.Core.Services.Mock;
using Flexispace.Web.Components;
using Flexispace.Web.Services;
using Flexispace.Web.ViewModels;

// Flexispace.Web entry point: registers Blazor Server, mock services, ViewModels, and routes.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<MockDataStore>();
builder.Services.AddSingleton<IAuthService, MockAuthService>();
builder.Services.AddSingleton<INotificationService, MockNotificationService>();
builder.Services.AddSingleton<IRoomService, MockRoomService>();
builder.Services.AddSingleton<IBookingService, MockBookingService>();
builder.Services.AddSingleton<IAdminService, MockAdminService>();
builder.Services.AddScoped<INavigationService, NavigationService>();
builder.Services.AddSingleton<AuthStateNotifier>();

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

app.Services.GetRequiredService<AuthStateNotifier>().Initialize();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
