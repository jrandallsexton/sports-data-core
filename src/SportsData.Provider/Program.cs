
using Hangfire;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.DependencyInjection;
using SportsData.Core.Middleware.Health;
using SportsData.Provider.Infrastructure.Data;

using System.Reflection;

namespace SportsData.Provider
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();

            builder.Services.AddHealthChecks(Assembly.GetExecutingAssembly().GetName(false).Name);

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            //builder.Services.ConfigureHangfire(builder.Configuration);

            var config = builder.Configuration["ConnectionStrings:ProviderDataContext"];

            //builder.Services.AddDbContext<ProviderDataContext>(options =>
            //{
            //    options.EnableSensitiveDataLogging();
            //    options.UseNpgsql(config, b => b.MigrationsAssembly("SportsData.Provider"));
            //});

            //await using var serviceProvider = builder.Services.BuildServiceProvider();
            //var context = serviceProvider.GetRequiredService<ProviderDataContext>();
            //await context.Database.MigrateAsync();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //if (app.Environment.IsDevelopment())
            //{
            app.UseSwagger();
            app.UseSwaggerUI();
            //app.UseHangfireDashboard("/dashboard", new DashboardOptions
            //{
            //    Authorization = new[] { new DashboardAuthFilter() }
            //});
            //}

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseHealthChecks("/health", new HealthCheckOptions()
            {
                ResponseWriter = HealthCheckWriter.WriteResponse
            });

            app.MapControllers();

            await app.RunAsync();
        }
    }
}
