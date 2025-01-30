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
                await LoadSeedData(context, mode);
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

        private static async Task SeedSeasonalResourceIndexes(AppDataContext dbContext, Sport sport, string league, int seasonYear, int index)
        {
            /* Groups By Season (Conferences) */
            if (sport == Sport.FootballNcaa)
            {
                await dbContext.Resources.AddAsync(new ResourceIndex()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SportId = sport,
                    DocumentType = DocumentType.GroupBySeason,
                    Endpoint = $"http://sports.core.api.espn.com/v2/sports/football/leagues/{league}/seasons/{seasonYear}/types/3/groups/80/children",
                    EndpointMask = null,
                    CreatedBy = Guid.Empty,
                    IsSeasonSpecific = true,
                    IsEnabled = true,
                    SeasonYear = seasonYear,
                    Ordinal = index
                });
            }
            else
            {
                await dbContext.Resources.AddAsync(new ResourceIndex()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SportId = sport,
                    DocumentType = DocumentType.GroupBySeason,
                    Endpoint = $"http://sports.core.api.espn.com/v2/sports/football/leagues/{league}/seasons/2024/types/",
                    EndpointMask = null,
                    CreatedBy = Guid.Empty,
                    IsSeasonSpecific = true,
                    IsEnabled = true,
                    SeasonYear = seasonYear,
                    Ordinal = index
                });
            }

            index++;

            /* Teams By Season */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.TeamBySeason,
                Endpoint = $"http://sports.core.api.espn.com/v2/sports/football/leagues/{league}/seasons/{seasonYear}/teams",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsSeasonSpecific = true,
                IsEnabled = true,
                SeasonYear = seasonYear,
                Ordinal = index
            });

            index++;

            /* Athletes By Season */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.AthleteBySeason,
                Endpoint = $"http://sports.core.api.espn.com/v2/sports/football/leagues/{league}/seasons/{seasonYear}/athletes",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsSeasonSpecific = true,
                IsEnabled = true,
                SeasonYear = seasonYear,
                Ordinal = index
            });

            index++;

            /* Coaches By Season */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.CoachBySeason,
                Endpoint = $"http://sports.core.api.espn.com/v2/sports/football/leagues/{league}/seasons/{seasonYear}/coaches",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsSeasonSpecific = true,
                IsEnabled = false,
                SeasonYear = seasonYear,
                Ordinal = index
            });

            index++;
        }

        private static async Task SeedNonSeasonalResourceIndexes(AppDataContext dbContext, Sport sport, string league, int index)
        {
            /* Venues */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.Venue,
                Endpoint = $"http://sports.core.api.espn.com/v2/sports/football/leagues/{league}/venues",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsEnabled = true,
                Ordinal = index
            });

            index++;

            /* Franchises */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.Franchise,
                Endpoint = $"http://sports.core.api.espn.com/v2/sports/football/leagues/{league}/franchises",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsEnabled = true,
                Ordinal = index
            });

            index++;

            /* Positions */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.Position,
                Endpoint = $"http://sports.core.api.espn.com/v2/sports/football/leagues/{league}/positions",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsEnabled = true,
                Ordinal = index
            });

            /* Athletes */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.Athlete,
                Endpoint = $"http://sports.core.api.espn.com/v2/sports/football/leagues/{league}/athletes",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsSeasonSpecific = true,
                IsEnabled = true,
                Ordinal = index
            });

            index++;
        }

        private static async Task LoadSeedData(AppDataContext dbContext, Sport mode)
        {
            if (await dbContext.Resources.AnyAsync())
                return;

            var index = 0;

            switch (mode)
            {
                case Sport.FootballNcaa:
                    await SeedNonSeasonalResourceIndexes(dbContext, Sport.FootballNcaa, "college-football", index);
                    await SeedSeasonalResourceIndexes(dbContext, Sport.FootballNcaa, "college-football", 2023, index);
                    await SeedSeasonalResourceIndexes(dbContext, Sport.FootballNcaa, "college-football", 2024, index);
                    break;
                case Sport.FootballNfl:
                    await SeedNonSeasonalResourceIndexes(dbContext, Sport.FootballNfl, "nfl", index);
                    await SeedSeasonalResourceIndexes(dbContext, Sport.FootballNfl, "nfl", 2023, index);
                    await SeedSeasonalResourceIndexes(dbContext, Sport.FootballNfl, "nfl", 2024, index);
                    break;
                case Sport.All:
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            await dbContext.SaveChangesAsync();
        }
    }
}
