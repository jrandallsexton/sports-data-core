using Hangfire;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Application.Contests.Overview;
using SportsData.Producer.Application.Documents.Processors;
using SportsData.Producer.Application.Franchises;
using SportsData.Producer.Application.FranchiseSeasonRankings;
using SportsData.Producer.Application.FranchiseSeasons;
using SportsData.Producer.Application.GroupSeasons;
using SportsData.Producer.Application.Images;
using SportsData.Producer.Application.SeasonWeek;
using SportsData.Producer.Application.Venues;
using SportsData.Producer.Config;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Geo;

namespace SportsData.Producer.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(
            this IServiceCollection services,
            Sport mode)
        {
            // Register document processing configuration
            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                
                // Read from Azure App Configuration: SportsData.Producer:DocumentProcessing:EnableDependencyRequests
                var enableDependencyRequestsValue = config["SportsData.Producer:DocumentProcessing:EnableDependencyRequests"];
                var enableDependencyRequests = bool.TryParse(enableDependencyRequestsValue, out var parsed) 
                    ? parsed 
                    : false; // Default to false (safe mode: no reactive requests)
                
                return new DocumentProcessingConfig
                {
                    EnableDependencyRequests = enableDependencyRequests
                };
            });

            services.AddScoped<IDataContextFactory, DataContextFactory>();

            services.AddDataPersistenceExternal();

            services.AddScoped<DocumentCreatedProcessor>();

            services.AddSingleton<IDocumentProcessorRegistry, DocumentProcessorRegistry>();
            services.Scan(scan => scan
                .FromAssemblyOf<IProcessDocuments>()
                .AddClasses(c => c.AssignableTo<IProcessDocuments>())
                .AsSelfWithInterfaces()
                .WithScopedLifetime());

            services.AddScoped<IDocumentProcessorFactory>(provider =>
            {
                // Sport-specific factory registration
                // For Football: Use FootballDataContext with outbox support
                // For Golf: Use GolfDataContext with outbox support
                // For Basketball: Use BasketballDataContext with outbox support
                // The generic factory ensures the correct concrete DbContext type is passed to processors,
                // which enables the MassTransit outbox interceptor for transactional event publishing.
                
                var context = provider.GetRequiredService<FootballDataContext>();
                var logger = provider.GetRequiredService<ILogger<DocumentProcessorFactory<FootballDataContext>>>();
                var registry = provider.GetRequiredService<IDocumentProcessorRegistry>();
                var factory = new DocumentProcessorFactory<FootballDataContext>(provider, logger, context, registry);
                return factory;
            });

            services.AddScoped<IImageProcessorFactory>(provider =>
            {
                // Sport-specific factory registration (same pattern as DocumentProcessorFactory)
                var appMode = provider.GetRequiredService<IAppMode>();
                var context = provider.GetRequiredService<FootballDataContext>();
                var logger = provider.GetRequiredService<ILogger<ImageProcessorFactory<FootballDataContext>>>();
                var decoder = provider.GetRequiredService<IDecodeDocumentProvidersAndTypes>();

                return new ImageProcessorFactory<FootballDataContext>(appMode, decoder, provider, context, logger);
            });

            services.AddScoped<ImageRequestedProcessor>();
            services.AddScoped<ImageProcessedProcessor>();

            services.AddScoped<IProcessImageRequests, ImageRequestedProcessor>();
            services.AddScoped<IProcessProcessedImages, ImageProcessedProcessor>();

            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();

            services.AddScoped<IEnrichContests, ContestEnrichmentProcessor>();
            services.AddScoped<ContestEnrichmentJob>();

            services.AddScoped<IEnrichFranchiseSeasons, FranchiseSeasonEnrichmentProcessor<TeamSportDataContext>>();
            services.AddScoped<FranchiseSeasonEnrichmentJob>();

            services.AddScoped<IUpdateContests, ContestUpdateProcessor>();
            services.AddScoped<ContestUpdateJob>();

            services.AddScoped<IContestOverviewService, ContestOverviewService>();

            services.AddScoped<ISeasonWeekService, SeasonWeekService>();

            services.AddScoped<ICompetitionMetricService, CompetitionMetricsService>();
            services.AddScoped<ICompetitionService, CompetitionService>();

            services.AddScoped<IFranchiseSeasonMetricsService, FranchiseSeasonMetricsService>();

            services.AddScoped<IGroupSeasonsService, GroupSeasonsService>();

            services.AddScoped<IContestReplayService, ContestReplayService>();

            services.AddScoped<IFootballCompetitionBroadcastingJob, FootballCompetitionStreamer>();

            services.AddScoped<IFranchiseSeasonRankingService, FranchiseSeasonRankingService>();

            services.AddScoped<IVenueService, VenueService>();
            services.AddScoped<VenueGeoCodeJob>();
            services.AddScoped<IGeocodingService, GeoCodingService>();

            services.AddScoped<FootballCompetitionMetricsAuditJob>();

            return services;
        }

        public static IServiceProvider ConfigureHangfireJobs(
            this IServiceProvider services,
            Sport mode)
        {
            var serviceScope = services.CreateScope();

            var recurringJobManager = serviceScope.ServiceProvider
                .GetRequiredService<IRecurringJobManager>();

            recurringJobManager.AddOrUpdate<ContestEnrichmentJob>(
                nameof(ContestEnrichmentJob),
                job => job.ExecuteAsync(),
                Cron.Weekly);

            recurringJobManager.AddOrUpdate<ContestUpdateJob>(
                nameof(ContestUpdateJob),
                job => job.ExecuteAsync(),
                Cron.Daily);

            recurringJobManager.AddOrUpdate<FootballCompetitionMetricsAuditJob>(
                nameof(FootballCompetitionMetricsAuditJob),
                job => job.ExecuteAsync(),
                "0 7 * * 0"); // Sunday at 07:00 UTC

            recurringJobManager.AddOrUpdate<FootballCompetitionStreamScheduler>(
                nameof(FootballCompetitionStreamScheduler),
                job => job.Execute(),
                "0 7 * * 0"); // Sunday at 07:00 UTC

            recurringJobManager.AddOrUpdate<FranchiseSeasonEnrichmentJob>(
                nameof(FranchiseSeasonEnrichmentJob),
                job => job.ExecuteAsync(),
                Cron.Weekly);

            recurringJobManager.AddOrUpdate<VenueGeoCodeJob>(
                nameof(VenueGeoCodeJob),
                job => job.ExecuteAsync(),
                "0 7 * * 0"); // Sunday at 07:00 UTC

            return services;
        }
    }
}
