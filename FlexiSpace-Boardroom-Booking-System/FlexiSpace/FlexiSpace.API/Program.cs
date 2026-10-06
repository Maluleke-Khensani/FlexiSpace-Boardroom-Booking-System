using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;
using FlexiSpace.API.Controllers;
using FlexiSpace.API.Services;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using FlexiSpace.Infrastructure.Seed;
using FlexiSpace.Infrastructure.services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;

namespace FlexiSpace.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddAuthorization();
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(
                        new JsonStringEnumConverter());
                });
            builder.Services.AddHttpContextAccessor();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowClients", policy =>
                    policy.SetIsOriginAllowed(static origin =>
                          {
                              if (string.IsNullOrWhiteSpace(origin)) return false;
                              if (origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase)) return true;
                              if (origin.StartsWith("https://localhost:", StringComparison.OrdinalIgnoreCase)) return true;
                              if (origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase)) return true;
                              return false;
                          })
                          .AllowAnyHeader()
                          .AllowAnyMethod());
            });

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")));

            var hasGraph = HasConfig(builder.Configuration,
                "MicrosoftGraph:TenantId",
                "MicrosoftGraph:ClientId",
                "MicrosoftGraph:ClientSecret");

            var hasEntraApp = HasConfig(builder.Configuration,
                "AzureAd:TenantId",
                "AzureAd:ClientId");

            // Graph app credentials (may differ from the public SPA/API client id used for JWT).
            if (hasGraph)
            {
                builder.Services.AddScoped<IEntraUserService>(sp =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    return new EntraUserService(
                        configuration["MicrosoftGraph:TenantId"]!,
                        configuration["MicrosoftGraph:ClientId"]!,
                        configuration["MicrosoftGraph:ClientSecret"]!);
                });
            }
            else
            {
                builder.Services.AddScoped<IEntraUserService, NoOpEntraUserService>();
            }

            if (hasGraph && !string.IsNullOrWhiteSpace(builder.Configuration["MicrosoftGraph:SenderEmail"]))
            {
                builder.Services.AddScoped<IEmailService>(sp =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    return new MicrosoftGraphEmailService(
                        configuration["MicrosoftGraph:TenantId"]!,
                        configuration["MicrosoftGraph:ClientId"]!,
                        configuration["MicrosoftGraph:ClientSecret"]!,
                        configuration["MicrosoftGraph:SenderEmail"]!);
                });

                builder.Services.AddScoped<ICalendarService>(sp =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    return new MicrosoftGraphCalendarService(
                        configuration["MicrosoftGraph:TenantId"]!,
                        configuration["MicrosoftGraph:ClientId"]!,
                        configuration["MicrosoftGraph:ClientSecret"]!);
                });
            }
            else
            {
                builder.Services.AddScoped<IEmailService, NoOpEmailService>();
                builder.Services.AddScoped<ICalendarService, NoOpCalendarService>();
            }

            // Dual JWT: Entra ID (when configured) + DevAuth (Development demo tiles).
            const string smartScheme = "Smart";
            var authBuilder = builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = smartScheme;
                options.DefaultChallengeScheme = smartScheme;
            });

            authBuilder.AddPolicyScheme(smartScheme, smartScheme, options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var header = context.Request.Headers.Authorization.ToString();
                    if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var token = header["Bearer ".Length..].Trim();
                        var handler = new JwtSecurityTokenHandler();
                        if (handler.CanReadToken(token))
                        {
                            var jwt = handler.ReadJwtToken(token);
                            if (string.Equals(jwt.Issuer, DevAuthController.Issuer, StringComparison.Ordinal))
                                return DevAuthController.SchemeName;
                        }
                    }

                    return hasEntraApp
                        ? JwtBearerDefaults.AuthenticationScheme
                        : DevAuthController.SchemeName;
                };
            });

            authBuilder.AddJwtBearer(DevAuthController.SchemeName, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = DevAuthController.Issuer,
                    ValidateAudience = true,
                    ValidAudience = DevAuthController.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(DevAuthController.GetSigningKey(builder.Configuration)),
                    ValidateLifetime = true,
                    RoleClaimType = "roles",
                    NameClaimType = "preferred_username"
                };
            });

            if (hasEntraApp)
            {
                authBuilder.AddMicrosoftIdentityWebApi(options =>
                {
                    builder.Configuration.Bind("AzureAd", options);
                    options.TokenValidationParameters.ValidateIssuer = false;
                    options.TokenValidationParameters.RoleClaimType = "roles";

                    // One API for web (SPA) + mobile (public client): accept both app ids as audience.
                    // Khumo's TestClient uses 77163347…; Khensani's mobile/Graph app is 85378c65….
                    var audiences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    void AddAudience(string? id)
                    {
                        if (string.IsNullOrWhiteSpace(id)) return;
                        audiences.Add(id);
                        audiences.Add($"api://{id}");
                    }

                    AddAudience(builder.Configuration["AzureAd:ClientId"]);
                    AddAudience(builder.Configuration["AzureAd:Audience"]);
                    AddAudience(builder.Configuration["AzureAd:SpaClientId"]);
                    AddAudience(builder.Configuration["AzureAd:MobileClientId"]);
                    // Known FlexiSpace registrations (safe defaults if secrets omit the extras).
                    AddAudience("77163347-59be-48f4-8675-535af30a3a53");
                    AddAudience("85378c65-ead1-4b71-956a-389142bd3342");
                    AddAudience("300b9a6d-f490-4342-9cd0-a0f9bc23f2b5");

                    if (audiences.Count > 0)
                        options.TokenValidationParameters.ValidAudiences = audiences;
                }, options => { builder.Configuration.Bind("AzureAd", options); });
            }

            builder.Services.AddScoped<ILocationService, LocationService>();
            builder.Services.AddScoped<ILocationCalendarAccountService, LocationCalendarAccountService>();
            builder.Services.AddScoped<IBoardroomService, BoardroomService>();
            builder.Services.AddScoped<IEquipmentService, EquipmentService>();
            builder.Services.AddScoped<IBookingService, BookingService>();
            builder.Services.AddHttpClient<IAiRecommendationService, AiRecommendationService>();
            builder.Services.AddHttpClient(nameof(EntraPasswordLoginService));
            builder.Services.AddSingleton<EntraPasswordLoginService>();
            builder.Services.AddScoped<ICateringService, CateringService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
            builder.Services.AddScoped<IAuditService, AuditService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();

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
                    Description = "Paste a DevAuth or Entra access token. Swagger adds the Bearer prefix."
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

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await context.Database.MigrateAsync();
                await DatabaseSeeder.SeedAsync(context);
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Keep HTTP usable for local MAUI / curl smoke tests in Development.
            if (!app.Environment.IsDevelopment())
                app.UseHttpsRedirection();

            app.UseCors("AllowClients");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            await app.RunAsync();
        }

        private static bool HasConfig(IConfiguration config, params string[] keys) =>
            keys.All(k => !string.IsNullOrWhiteSpace(config[k]));
    }
}
