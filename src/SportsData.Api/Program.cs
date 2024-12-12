
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using System.Text;
using SportsData.Core.Middleware.Health;

namespace SportsData.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddHealthChecks();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //if (app.Environment.IsDevelopment())
            //{
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                var links = new StringBuilder();
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
