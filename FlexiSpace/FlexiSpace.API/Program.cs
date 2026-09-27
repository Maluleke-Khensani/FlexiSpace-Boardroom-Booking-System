using FlexiSpace.Core.Common;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using FlexiSpace.Infrastructure.Seed;
using FlexiSpace.Infrastructure.services;
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

            // TEMP — local Swagger testing only. Revert before committing.
            // Configure authentication using Microsoft Identity Web API
            // with Bearer token authentication.
            // builder.Services.AddAuthentication("Bearer")
            //     .AddMicrosoftIdentityWebApi(
            //         builder.Configuration.GetSection("AzureAd"));

            // TEMP — local Swagger testing only. Revert before committing.
            // Authorization
            // builder.Services.AddAuthorization();

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
            // as the database provider.
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")));

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

            // Sends outbound email (e.g. "booking created" alerts to Centre
            // Managers) via the same app-only Graph credentials used for
            // calendar sync above. Needs the Mail.Send Application
            // permission granted on that app registration, plus a
            // MicrosoftGraph:SenderEmail mailbox to send from (a shared
            // mailbox like notifications@flexispace.net.za, not a specific
            // person's inbox).
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

            // Seed database
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                await FlexiSpace.Infrastructure.Seed.DatabaseSeeder
                    .SeedUsersAsync(context);
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseCors("AllowReactTestClient");

            // TEMP — local Swagger testing only. Revert before committing.
            // Authentication must happen before authorization.
            // app.UseAuthentication();
            // app.UseAuthorization();

            app.MapControllers();

            await app.RunAsync();
        }
    }
}