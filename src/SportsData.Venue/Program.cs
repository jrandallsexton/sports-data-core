using Serilog;

using SportsData.Core.DependencyInjection;
using SportsData.Core.Middleware;
using SportsData.Venue.Data;

using System.Reflection;

namespace SportsData.Venue
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

            // Add Serilog
            builder.Host.UseSerilog((context, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration);
            });

            services.AddDataPersistence<AppDataContext>(config);
            services.AddHealthChecks<AppDataContext>(Assembly.GetExecutingAssembly().GetName(false).Name);

            builder.Services.AddMediatR(Assembly.GetExecutingAssembly());

            // Add Serilog
            builder.Host.UseSerilog((context, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration);
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            //app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseCommonFeatures();

            app.MapControllers();

            app.UseSerilogRequestLogging();

            app.UseMiddleware<ExceptionHandlingMiddleware>();

            app.Run();
        }
    }
}
