using Microsoft.EntityFrameworkCore;

using SportsData.Core.DependencyInjection;
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

            // TODO: Make this follow the same pattern as other services
            builder.Services.AddControllers();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // TODO: Find a way to move this to middleware for all services
            builder.Services.AddDbContext<ProviderDataContext>(options =>
            {
                options.EnableSensitiveDataLogging();
                options.UseSqlServer(builder.Configuration.GetConnectionString("AppDataContext"));
            });

            builder.Services.AddHealthChecks<ProviderDataContext>(Assembly.GetExecutingAssembly().GetName(false).Name);

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
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
                //app.UseHangfireDashboard("/dashboard", new DashboardOptions
                //{
                //    Authorization = new[] { new DashboardAuthFilter() }
                //});
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseHealthChecks();

            app.MapControllers();

            await app.RunAsync();
        }
    }
}
