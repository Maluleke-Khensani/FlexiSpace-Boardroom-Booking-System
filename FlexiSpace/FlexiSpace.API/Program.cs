using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection")));
               

            //Swagger to test my API endpoints and generate documentation for my API

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
