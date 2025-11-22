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
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Geo;

namespace SportsData.Producer.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(
            this IServiceCollection services,
            Sport mode)
        {
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
                var context = provider.GetRequiredService<BaseDataContext>();
                var logger = provider.GetRequiredService<ILogger<DocumentProcessorFactory>>();
                var registry = provider.GetRequiredService<IDocumentProcessorRegistry>();
                var factory = new DocumentProcessorFactory(provider, logger, context, registry);
                return factory;
            });

            services.AddScoped<IImageProcessorFactory>(provider =>
            {
                var appMode = provider.GetRequiredService<IAppMode>();
                var context = provider.GetRequiredService<BaseDataContext>();
                var logger = provider.GetRequiredService<ILogger<ImageProcessorFactory>>();
                var decoder = provider.GetRequiredService<IDecodeDocumentProvidersAndTypes>();

                return new ImageProcessorFactory(appMode, decoder, provider, context, logger);
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

            recurringJobManager.AddOrUpdate<FranchiseSeasonEnrichmentJob>(
                nameof(FranchiseSeasonEnrichmentJob),
                job => job.ExecuteAsync(),
                Cron.Weekly);

            recurringJobManager.AddOrUpdate<ContestUpdateJob>(
                nameof(ContestUpdateJob),
                job => job.ExecuteAsync(),
                Cron.Daily);

            recurringJobManager.AddOrUpdate<FootballCompetitionStreamScheduler>(
                nameof(FootballCompetitionStreamScheduler),
                job => job.Execute(),
                "0 7 * * 0"); // Sunday at 07:00 UTC

            recurringJobManager.AddOrUpdate<VenueGeoCodeJob>(
                nameof(VenueGeoCodeJob),
                job => job.ExecuteAsync(),
                "0 7 * * 0"); // Sunday at 07:00 UTC

            return services;
        }
    }
}
