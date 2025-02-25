using Hangfire;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Config;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Provider.Config;
using SportsData.Provider.DependencyInjection;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Data.Seeders;
using SportsData.Provider.Middleware.Health;

namespace SportsData.Provider
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var mode = (args.Length > 0 && args[0] == "-mode") ?
                Enum.Parse<Sport>(args[1]) :
                Sport.All;

            var builder = WebApplication.CreateBuilder(args);
            builder.UseCommon();

            // Add services to the container.
            var config = builder.Configuration;
            config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName, mode);

            var services = builder.Services;
            services.AddCoreServices(config);
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddProviders(config);
            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName);
            services.AddSingleton<DocumentService>();

            //services.AddMessaging(config);

            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();

                x.UsingAzureServiceBus((context, cfg) =>
                {
                    cfg.Host(config[CommonConfigKeys.AzureServiceBus]);

                    //cfg.Message<DocumentCreated>(x =>
                    //{
                    //    const string entityName = $"{nameof(DocumentCreated)}.FootballNcaa";
                    //    x.SetEntityName(entityName.ToLower());
                    //});

                    cfg.ConfigureJsonSerializerOptions(o =>
                    {
                        o.IncludeFields = true;
                        return o;
                    });
                    cfg.ConfigureEndpoints(context);
                });
            });

            services.AddInstrumentation(builder.Environment.ApplicationName);

            services.AddHangfire(x => x.UseSqlServerStorage(config[$"{builder.Environment.ApplicationName}:ConnectionStrings:Hangfire"]));

            builder.Services.Configure<ProviderDocDatabase>(
                builder.Configuration.GetSection($"{builder.Environment.ApplicationName}:ProviderDocDatabase"));

            services.AddHangfireServer(serverOptions =>
            {
                // https://codeopinion.com/scaling-hangfire-process-more-jobs-concurrently/
                serverOptions.WorkerCount = 50;
            });

            services.AddHealthChecks<AppDataContext, Program>(builder.Environment.ApplicationName);
            services.AddHealthChecks().AddCheck<DocumentDatabaseHealthCheck>(nameof(DocumentDatabaseHealthCheck));

            services.AddLocalServices(mode);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //app.UseHttpsRedirection();

            using (var scope = app.Services.CreateScope())
            {
                var appServices = scope.ServiceProvider;
                var context = appServices.GetRequiredService<AppDataContext>();
                await context.Database.MigrateAsync();
                await LoadSeedData(context, mode);
            }

            app.UseHangfireDashboard("/dashboard", new DashboardOptions
            {
                Authorization = [new DashboardAuthFilter()]
            });

            app.UseAuthorization();

            app.UseCommonFeatures();

            app.MapControllers();

            //await app.Services.ConfigureHangfireJobs(mode);

            await app.RunAsync();
        }
        
        private static async Task LoadSeedData(AppDataContext dbContext, Sport mode)
        {
            if (await dbContext.Resources.AnyAsync())
                return;

            switch (mode)
            {
                case Sport.FootballNcaa:
                case Sport.FootballNfl:
                    var footballValues = new FootballSeeder().Generate(mode, [2023, 2024]);
                    await dbContext.Resources.AddRangeAsync(footballValues);
                    await dbContext.SaveChangesAsync();
                    break;
                case Sport.GolfPga:
                    var golfValues = new GolfSeeder().Generate(mode, [2024]);
                    await dbContext.Resources.AddRangeAsync(golfValues);
                    await dbContext.SaveChangesAsync();
                    break;
                case Sport.BasketballNba:
                    var basketballValues = new BasketballSeeder().Generate(mode, [2024]);
                    await dbContext.Resources.AddRangeAsync(basketballValues);
                    await dbContext.SaveChangesAsync();
                    break;
                case Sport.BaseballMlb:
                    break;
                case Sport.All:
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
    }
}
