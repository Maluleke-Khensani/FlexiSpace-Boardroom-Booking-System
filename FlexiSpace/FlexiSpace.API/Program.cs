using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Services;


namespace FlexiSpace.API
{

    public class Program
    {
        public static void Main(string[] args)
        {
            //creating a new ASP.NET application
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            //My Application will have an API controller
            builder.Services.AddControllers();

            // Register the database context and configure SQL Server as the database provider.
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddScoped<ILocationService, LocationService>();


            // Register Swagger services to generate API documentation and allow endpoint testing during development.
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();


            //I've finished configuring everything. Now build the application.
            var app = builder.Build();


            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();


            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
