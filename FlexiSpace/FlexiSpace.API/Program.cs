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
            builder.Services.AddControllers();


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

<<<<<<< Updated upstream
=======
            builder.Services.AddScoped<IGraphDirectoryService>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();

                return new GraphDirectoryService(
                    configuration["MicrosoftGraph:TenantId"]!,
                    configuration["MicrosoftGraph:ClientId"]!,
                    configuration["MicrosoftGraph:ClientSecret"]!
                );
            });

            builder.Services.AddScoped<ICalendarService>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();

                return new MicrosoftGraphCalendarService(
                    configuration["MicrosoftGraph:TenantId"]!,
                    configuration["MicrosoftGraph:ClientId"]!,
                    configuration["MicrosoftGraph:ClientSecret"]!
                );
            });

>>>>>>> Stashed changes
            // Register application services.
            builder.Services.AddScoped<ILocationService, LocationService>();
            builder.Services.AddScoped<IBoardroomService, BoardroomService>();
            builder.Services.AddScoped<IEquipmentService, EquipmentService>();


            // Register Swagger services to generate API documentation and allow endpoint testing during development.
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // After builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactTestClient", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
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