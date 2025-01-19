using Hangfire;
using Microsoft.EntityFrameworkCore;
using SportsData.Core.Common;
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
            config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName);

            var services = builder.Services;
            services.AddCoreServices(config);
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddProviders(config);

            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName);
            services.AddSingleton<DocumentService>();
            services.AddMessaging(config);
            services.AddInstrumentation(builder.Environment.ApplicationName);

            services.AddHangfire(x => x.UseSqlServerStorage(config[$"{builder.Environment.ApplicationName}:ConnectionStrings:Hangfire"]));

            builder.Services.Configure<ProviderDocDatabase>(
                builder.Configuration.GetSection($"{builder.Environment.ApplicationName}:ProviderDocDatabase"));

            services.AddHangfireServer(serverOptions =>
            {
                // https://codeopinion.com/scaling-hangfire-process-more-jobs-concurrently/
                serverOptions.WorkerCount = 10;
            });

            services.AddHealthChecks<AppDataContext, Program>(builder.Environment.ApplicationName);
            services.AddHealthChecks().AddCheck<DocumentDatabaseHealthCheck>(nameof(DocumentDatabaseHealthCheck));

            services.AddLocalServices(mode);

            await services.ApplyMigrations<AppDataContext>(LoadSeedData);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //app.UseHttpsRedirection();

            app.UseHangfireDashboard("/dashboard", new DashboardOptions
            {
                Authorization = new[] { new DashboardAuthFilter() }
            });

            app.UseAuthorization();

            app.UseCommonFeatures();

            app.MapControllers();

            await app.Services.ConfigureHangfireJobs(mode);

            await app.RunAsync();
        }

        private static async Task LoadSeedData(AppDataContext dbContext)
        {
            if (await dbContext.Resources.AnyAsync())
                return;

            /* Venues */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                ProviderId = SourceDataProvider.Espn,
                SportId = Sport.FootballNcaa,
                DocumentTypeId = DocumentType.Venue,
                Endpoint = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues?lang=en&limit=999",
                EndpointMask = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues/",
                CreatedBy = Guid.Empty
            });

            /* Franchises */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                ProviderId = SourceDataProvider.Espn,
                SportId = Sport.FootballNcaa,
                DocumentTypeId = DocumentType.Franchise,
                Endpoint = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises?lang=en&limit=999",
                EndpointMask = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/",
                CreatedBy = Guid.Empty
            });

            /* Groups By Season (Conferences) */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                ProviderId = SourceDataProvider.Espn,
                SportId = Sport.FootballNcaa,
                DocumentTypeId = DocumentType.GroupBySeason,
                Endpoint = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/types/3/groups/80/children?lang=en&limit=999",
                EndpointMask = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/types/3/groups/",
                CreatedBy = Guid.Empty,
                SeasonYear = 2024
            });

            /* Teams By Season */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                ProviderId = SourceDataProvider.Espn,
                SportId = Sport.FootballNcaa,
                DocumentTypeId = DocumentType.TeamBySeason,
                Endpoint = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams?lang=en&limit=999",
                EndpointMask = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/",
                CreatedBy = Guid.Empty,
                SeasonYear = 2024
            });

            /* Athletes By Season */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                ProviderId = SourceDataProvider.Espn,
                SportId = Sport.FootballNcaa,
                DocumentTypeId = DocumentType.AthleteBySeason,
                Endpoint = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes?lang=en&limit=100000",
                EndpointMask = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/",
                CreatedBy = Guid.Empty,
                SeasonYear = 2024
            });

            /* Coaches By Season */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                ProviderId = SourceDataProvider.Espn,
                SportId = Sport.FootballNcaa,
                DocumentTypeId = DocumentType.CoachBySeason,
                Endpoint = @"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/coaches?lang=en&limit=999",
                EndpointMask = @"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/coaches/",
                CreatedBy = Guid.Empty,
                SeasonYear = 2024
            });

            /* NFL */
            /* Venues */
            await dbContext.Resources.AddAsync(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                ProviderId = SourceDataProvider.Espn,
                SportId = Sport.FootballNfl,
                DocumentTypeId = DocumentType.Venue,
                Endpoint = "http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/venues?lang=en&limit=999",
                EndpointMask = "http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/venues/",
                CreatedBy = Guid.Empty
            });
        }
    }
}
