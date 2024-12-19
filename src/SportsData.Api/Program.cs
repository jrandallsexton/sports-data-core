
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using SportsData.Core.DependencyInjection;
using SportsData.Core.Middleware.Health;

using System.Reflection;
using System.Text;
using SportsData.Core.Infrastructure.Clients.Venue;

namespace SportsData.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var config = builder.Configuration;
            var services = builder.Services;

            services.AddCoreServices(config);
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddProviders(config);
            services.AddHealthChecksMaster(Assembly.GetExecutingAssembly().GetName(false).Name);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            
            //if (app.Environment.IsDevelopment())
            //{
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                var links = new StringBuilder();
                links.AppendLine("<a href=\"/health\" target=\"_blank\">HealthCheck</a></br>");
                links.AppendLine("<a href=\"http://localhost:15672/#/\" target=\"_blank\">RabbitMQ</a></br>");
                links.AppendLine("<a href=\"http://localhost:8081/#/events?range=1d\" target=\"_blank\">Seq</a></br>");
                links.AppendLine("<a href=\"http://localhost:8888\" target=\"_blank\">pgAdmin</a></br>");
                options.HeadContent = links.ToString();
            });
            //}

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseHealthChecks("/health", new HealthCheckOptions()
            {
                ResponseWriter = HealthCheckWriter.WriteResponse
            });

            app.MapControllers();

            app.Run();
        }
    }
}
