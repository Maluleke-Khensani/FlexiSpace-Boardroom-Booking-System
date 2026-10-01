using FlexiSpace.Core.Common;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using FlexiSpace.Infrastructure.Seed;
using FlexiSpace.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;


namespace FlexiSpace.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Creating a new ASP.NET application
            var builder = WebApplication.CreateBuilder(args);

            // Configure authentication using Microsoft Identity Web API
            // with Bearer token authentication.
            builder.Services.AddAuthentication("Bearer")
                .AddMicrosoftIdentityWebApi(
                    builder.Configuration.GetSection("AzureAd"));

            // Authorization
            builder.Services.AddAuthorization();

            // Add services to the container.

            // My application will have API controllers.
            // Enums are serialized as their string names (e.g. "Confirmed"),
            // not the underlying int - the clients' own enums don't share
            // the same ordinal positions as this one, and never reliably
            // will once either side adds/removes/reorders a value, so
            // number-based serialization is a silent correctness bug
            // waiting to happen rather than a one-time mismatch to patch up.
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(
                        new System.Text.Json.Serialization.JsonStringEnumConverter());
                });

            // Needed by CurrentUserService to read claims off the current
            // request outside of a controller (it's injected into a
            // Scoped service, not a controller, so it can't just take
            // HttpContext directly).
            builder.Services.AddHttpContextAccessor();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowReactTestClient", policy =>
                    policy.WithOrigins("http://localhost:5173")
                          .AllowAnyHeader()
                          .AllowAnyMethod());
            });

            // Register the database context and configure SQL Server
            // as the database provider. The connection string is NOT kept in
            // appsettings (see DATABASE_BACKEND_HANDOVER.md, section 15):
            // each developer sets their own in user secrets, and Azure sets
            // it in App Service configuration.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection is not set. Run (from the repo root): " +
                    "dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<your connection string>\" " +
                    "--project FlexiSpace/FlexiSpace.API");
            }

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            builder.Services.AddScoped<IEntraUserService>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();

                var tenantId = configuration["AzureAd:TenantId"]
                    ?? throw new InvalidOperationException("AzureAd:TenantId is missing.");

                var clientId = configuration["AzureAd:ClientId"]
                    ?? throw new InvalidOperationException("AzureAd:ClientId is missing.");

                var clientSecret = configuration["AzureAd:ClientSecret"]
                    ?? throw new InvalidOperationException("AzureAd:ClientSecret is missing.");

                return new EntraUserService(
                    tenantId,
                    clientId,
                    clientSecret);
            });

            // MicrosoftGraph credentials aren't configured yet in any
            // environment this runs in (appsettings has no MicrosoftGraph
            // section). Registering MicrosoftGraphCalendarService/
            // MicrosoftGraphEmailService directly with null values throws
            // inside ClientSecretCredential's constructor - which runs
            // inside the DI factory, so it would fail every single request
            // that needs IBookingService (which depends on both), not just
            // whichever endpoint actually tries to send a calendar event or
            // an email. Fall back to no-op implementations instead, so
            // missing config degrades gracefully until real credentials
            // land - see NullCalendarService/NullEmailService.
            var graphTenantId = builder.Configuration["MicrosoftGraph:TenantId"];
            var graphClientId = builder.Configuration["MicrosoftGraph:ClientId"];
            var graphClientSecret = builder.Configuration["MicrosoftGraph:ClientSecret"];
            var graphSenderEmail = builder.Configuration["MicrosoftGraph:SenderEmail"];

            var graphAppCredentialsConfigured =
                !string.IsNullOrWhiteSpace(graphTenantId)
                && !string.IsNullOrWhiteSpace(graphClientId)
                && !string.IsNullOrWhiteSpace(graphClientSecret);

            if (graphAppCredentialsConfigured)
            {
                builder.Services.AddScoped<ICalendarService>(sp =>
                    new MicrosoftGraphCalendarService(
                        graphTenantId!,
                        graphClientId!,
                        graphClientSecret!));
            }
            else
            {
                builder.Services.AddScoped<ICalendarService, NullCalendarService>();
            }

            // Sends outbound email - booking confirmations/updates/
            // cancellations and the 1-hour-before reminder to the booker
            // (BookingService, BookingReminderHostedService), "room
            // blocked" alerts to affected bookers (BoardroomService), and
            // "booking created"/"booking moved here" alerts to Centre
            // Managers (BookingService) - via the same app-only Graph
            // credentials used for calendar sync above. Needs the
            // Mail.Send Application permission granted on that app
            // registration, plus a MicrosoftGraph:SenderEmail mailbox to
            // send from (a real, licensed mailbox in the tenant - ideally a
            // shared one). SenderEmail is deliberately blank in
            // appsettings.json: set it in user secrets / App Service
            // settings. Until it's set, emails are skipped and a warning is
            // logged at startup (it used to hold a placeholder address,
            // which turned email ON and made every send fail).
            if (graphAppCredentialsConfigured && !string.IsNullOrWhiteSpace(graphSenderEmail))
            {
                builder.Services.AddScoped<IEmailService>(sp =>
                    new MicrosoftGraphEmailService(
                        graphTenantId!,
                        graphClientId!,
                        graphClientSecret!,
                        graphSenderEmail!));
            }
            else
            {
                builder.Services.AddScoped<IEmailService, NullEmailService>();
            }

            // Register application services.
            builder.Services.AddScoped<ILocationService, LocationService>();
            builder.Services.AddScoped<IBoardroomService, BoardroomService>();
            builder.Services.AddScoped<IEquipmentService, EquipmentService>();
            builder.Services.AddScoped<IBookingService, BookingService>();

            // Room blocking (maintenance, private events, etc.). Must be
            // registered or BlockedPeriodController can't be constructed.
            builder.Services.AddScoped<IBlockedPeriodService, BlockedPeriodService>();

            // Register AI recommendation service.
            builder.Services.AddHttpClient<IAiRecommendationService, AiRecommendationService>();

            // Fix: ICateringService and IUserService were being injected
            // into CateringController/UserController but were never
            // registered here - same bug class (and same fix) as the
            // missing IBookingService registration Tino found. Without
            // this, every request to those controllers throws
            // "Unable to resolve service for type ..." at runtime.
            builder.Services.AddScoped<ICateringService, CateringService>();
            builder.Services.AddScoped<IUserService, UserService>();

            // RBAC / admin / notifications (Denzel's scope).
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
            builder.Services.AddScoped<IAuditService, AuditService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddScoped<IReportingService, ReportingService>();

            // Push notifications (mobile only - see DeviceToken/NotificationService
            // comments). NotificationService takes a dependency on
            // IPushNotificationSender, so without this registration the app
            // throws on the very first notification (booking created, etc.)
            // - this was present in code but missing here.
            builder.Services.AddScoped<IDeviceTokenService, DeviceTokenService>();
            // Azure Notification Hubs (replaced the direct Firebase sender -
            // the client's architecture is Azure-only). A no-op until
            // NotificationHubs:ConnectionString and :HubName are set.
            builder.Services.AddScoped<IPushNotificationSender, AzureNotificationHubPushSender>();

            // Background job for all booking reminders: 24 hours and 2
            // hours before (in-app/push), and 1 hour before (email +
            // in-app). Polls every 5 minutes - see
            // BookingReminderHostedService for how it avoids double-
            // sending. A hosted service is a singleton by convention, so
            // it resolves its own DI scope per pass rather than taking any
            // Scoped service directly in its constructor. (This used to be
            // registered twice after a merge.)
            builder.Services.AddHostedService<BookingReminderHostedService>();


            // Register Swagger services to generate API documentation and allow endpoint testing during development.
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Description = "Paste your Entra ID access token here. Swagger UI will automatically add the Bearer prefix."
                });

                options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
            });
            // I've finished configuring everything. Now build the application.
            var app = builder.Build();

            // Say clearly at startup which outbound channels are off, instead
            // of failing quietly on every booking.
            if (!graphAppCredentialsConfigured || string.IsNullOrWhiteSpace(graphSenderEmail))
            {
                app.Logger.LogWarning(
                    "Email notifications are OFF: set MicrosoftGraph:TenantId, ClientId, ClientSecret and SenderEmail " +
                    "(user secrets or App Service settings). The app registration also needs the Mail.Send application permission.");
            }

            if (string.IsNullOrWhiteSpace(app.Configuration["NotificationHubs:ConnectionString"])
                || string.IsNullOrWhiteSpace(app.Configuration["NotificationHubs:HubName"]))
            {
                app.Logger.LogWarning(
                    "Mobile push notifications are OFF: set NotificationHubs:ConnectionString and NotificationHubs:HubName.");
            }

            // Seed the database - Development only, as the backend handover
            // (section 16) requires. In Azure, real reference data and the
            // first administrator are set up by hand instead.
            if (app.Environment.IsDevelopment())
            {
                using var scope = app.Services.CreateScope();
                var context = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                // Real sites, boardrooms, equipment and catering first, so the seeded
                // users are attached to a real location. Only adds what's
                // missing.
                await FlexiSpace.Infrastructure.Seed.ReferenceDataSeeder
                    .SeedLocationsAndBoardroomsAsync(context);

                // Seeded people come from configuration (SeedUsers:*), not
                // code - see DatabaseSeeder.
                await FlexiSpace.Infrastructure.Seed.DatabaseSeeder
                    .SeedUsersAsync(context, app.Configuration);
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseCors("AllowReactTestClient");

            // Authentication must happen before authorization.
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            await app.RunAsync();
        }
    }
}
