using Hangfire;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Config;
using SportsData.Core.DependencyInjection;
using SportsData.Provider.Config;
using SportsData.Provider.DependencyInjection;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Data.Entities;
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
                await LoadSeedData(context);
            }

            app.UseHangfireDashboard("/dashboard", new DashboardOptions
            {
                Authorization = [new DashboardAuthFilter()]
            });

            app.UseAuthorization();

            app.UseCommonFeatures();

            app.MapControllers();

            await app.Services.ConfigureHangfireJobs(mode);

            await app.RunAsync();
        }

        private static class ResourceIndexId
        {
            public static class FootballNcaa
            {
                public static readonly Guid Venue = new Guid("3CF7C759-8C15-4083-AC4F-3A661A7FE5D3");
                public static readonly Guid Franchise = new Guid("3DF7C759-8C15-4083-AC4F-3A661A7FE5D3");
                public static readonly Guid GroupBySeason = new Guid("3EF7C759-8C15-4083-AC4F-3A661A7FE5D3");
                public static readonly Guid TeamBySeason = new Guid("3FF7C759-8C15-4083-AC4F-3A661A7FE5D3");
                public static readonly Guid AthleteBySeason = new Guid("40F7C759-8C15-4083-AC4F-3A661A7FE5D3");
                public static readonly Guid CoachBySeason = new Guid("41F7C759-8C15-4083-AC4F-3A661A7FE5D3");
            }

            public static class FootballNfl
            {
                public static readonly Guid Venue = new Guid("4AF7C759-8C15-4083-AC4F-3A661A7FE5D3");
                public static readonly Guid Franchise = new Guid("4BF7C759-8C15-4083-AC4F-3A661A7FE5D3");
                public static readonly Guid GroupBySeason = new Guid("4CF7C759-8C15-4083-AC4F-3A661A7FE5D3");
                public static readonly Guid TeamBySeason = new Guid("4DF7C759-8C15-4083-AC4F-3A661A7FE5D3");
                public static readonly Guid AthleteBySeason = new Guid("4EF7C759-8C15-4083-AC4F-3A661A7FE5D3");
                public static readonly Guid CoachBySeason = new Guid("4FF7C759-8C15-4083-AC4F-3A661A7FE5D3");
            }
        }

        private static async Task SeedFootballNcaa(AppDataContext dbContext)
        {
            /* Venues */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = ResourceIndexId.FootballNcaa.Venue,
                Provider = SourceDataProvider.Espn,
                SportId = Sport.FootballNcaa,
                DocumentType = DocumentType.Venue,
                Endpoint = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues?lang=en&limit=999",
                EndpointMask = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues/",
                CreatedBy = Guid.Empty,
                IsEnabled = true,
                Ordinal = 0
            });

            /* Franchises */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = ResourceIndexId.FootballNcaa.Franchise,
                Provider = SourceDataProvider.Espn,
                SportId = Sport.FootballNcaa,
                DocumentType = DocumentType.Franchise,
                Endpoint = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises?lang=en&limit=999",
                EndpointMask = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/",
                CreatedBy = Guid.Empty,
                IsEnabled = true,
                Ordinal = 1
            });

            /* Groups By Season (Conferences) */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = ResourceIndexId.FootballNcaa.GroupBySeason,
                Provider = SourceDataProvider.Espn,
                SportId = Sport.FootballNcaa,
                DocumentType = DocumentType.GroupBySeason,
                Endpoint = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/types/3/groups/80/children?lang=en&limit=999",
                EndpointMask = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/types/3/groups/",
                CreatedBy = Guid.Empty,
                SeasonYear = 2024,
                IsEnabled = true,
                Ordinal = 2
            });

            /* Teams By Season */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = ResourceIndexId.FootballNcaa.TeamBySeason,
                Provider = SourceDataProvider.Espn,
                SportId = Sport.FootballNcaa,
                DocumentType = DocumentType.TeamBySeason,
                Endpoint = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams?lang=en&limit=900",
                EndpointMask = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/",
                CreatedBy = Guid.Empty,
                SeasonYear = 2024,
                IsEnabled = true,
                Ordinal = 3
            });

            /* Athletes By Season */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = ResourceIndexId.FootballNcaa.AthleteBySeason,
                Provider = SourceDataProvider.Espn,
                SportId = Sport.FootballNcaa,
                DocumentType = DocumentType.AthleteBySeason,
                Endpoint = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes?lang=en&limit=100000",
                EndpointMask = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/",
                CreatedBy = Guid.Empty,
                SeasonYear = 2024,
                IsEnabled = false,
                Ordinal = 4
            });

            /* Coaches By Season */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = ResourceIndexId.FootballNcaa.CoachBySeason,
                Provider = SourceDataProvider.Espn,
                SportId = Sport.FootballNcaa,
                DocumentType = DocumentType.CoachBySeason,
                Endpoint = @"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/coaches?lang=en&limit=999",
                EndpointMask = @"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/coaches/",
                CreatedBy = Guid.Empty,
                SeasonYear = 2024,
                IsEnabled = false,
                Ordinal = 5
            });
        }

        private static async Task SeedFootballNfl(AppDataContext dbContext)
        {
            /* Venues */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = ResourceIndexId.FootballNfl.Venue,
                Provider = SourceDataProvider.Espn,
                SportId = Sport.FootballNfl,
                DocumentType = DocumentType.Venue,
                Endpoint = "http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/venues?lang=en&limit=999",
                EndpointMask = "http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/venues/",
                CreatedBy = Guid.Empty,
                IsEnabled = true,
                Ordinal = 0
            });

            /* Franchises */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = ResourceIndexId.FootballNfl.Franchise,
                Provider = SourceDataProvider.Espn,
                SportId = Sport.FootballNfl,
                DocumentType = DocumentType.Franchise,
                Endpoint = "http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/franchises?lang=en&limit=999",
                EndpointMask = "http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/franchises/",
                CreatedBy = Guid.Empty,
                IsEnabled = true,
                Ordinal = 1
            });
        }

        private static async Task LoadSeedData(AppDataContext dbContext)
        {
            if (await dbContext.Resources.AnyAsync())
                return;

            await SeedFootballNcaa(dbContext);

            await SeedFootballNfl(dbContext);

            await dbContext.SaveChangesAsync();
        }
    }
}
