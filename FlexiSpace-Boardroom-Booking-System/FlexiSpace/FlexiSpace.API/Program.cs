using FlexiSpace.Core.Common;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using FlexiSpace.Infrastructure.Seed;
using FlexiSpace.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

          
            // Authorization
            builder.Services.AddAuthorization();

            // Add services to the container.

            // My application will have API controllers.
            builder.Services.AddControllers();

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


            // Register services for Entra ID and Microsoft Graph API integration.
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

            // Register the Microsoft Graph Email service for sending emails.

            builder.Services.AddScoped<IEmailService>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();

                return new MicrosoftGraphEmailService(
                    configuration["MicrosoftGraph:TenantId"]!,
                    configuration["MicrosoftGraph:ClientId"]!,
                    configuration["MicrosoftGraph:ClientSecret"]!,
                    configuration["MicrosoftGraph:SenderEmail"]!
                );
            });


            // Configure JWT Bearer authentication with Microsoft Identity Web API.
            // It validates tokens issued by Entra ID and allows tokens from personal/external Microsoft accounts.
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(options =>
            {
                builder.Configuration.Bind("AzureAd", options);
                options.TokenValidationParameters.ValidateIssuer = false; // Allows tokens from personal/external Microsoft accounts
                options.TokenValidationParameters.RoleClaimType = "roles";
            }, options => { builder.Configuration.Bind("AzureAd", options); });


            // Register the Microsoft Graph Calendar service for managing calendar events.
            builder.Services.AddScoped<ICalendarService>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();

                return new MicrosoftGraphCalendarService(
                    configuration["MicrosoftGraph:TenantId"]!,
                    configuration["MicrosoftGraph:ClientId"]!,
                    configuration["MicrosoftGraph:ClientSecret"]!
                );
            });

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

            // Authentication must happen before authorization.
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            await app.RunAsync();
        }
    }
}