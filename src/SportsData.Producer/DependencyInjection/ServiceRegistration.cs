using Hangfire;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions;
using SportsData.Producer.Application.Competitions.Commands.CalculateCompetitionMetrics;
using SportsData.Producer.Application.Competitions.Commands.EnqueueCompetitionMediaRefresh;
using SportsData.Producer.Application.Competitions.Commands.EnqueueCompetitionMetricsCalculation;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionDrives;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMedia;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMetrics;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Application.Contests.Queries.GetContestById;
using SportsData.Producer.Application.Contests.Queries.GetContestOverview;
using SportsData.Producer.Application.Documents.Processors;
using SportsData.Producer.Application.Franchises;
using SportsData.Producer.Application.Franchises.Queries.GetAllFranchises;
using SportsData.Producer.Application.Franchises.Queries.GetFranchiseById;
using SportsData.Producer.Application.Franchises.Queries.GetFranchiseSeasons;
using SportsData.Producer.Application.Franchises.Queries.GetSeasonContests;
using SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetCurrentPolls;
using SportsData.Producer.Application.FranchiseSeasons.Commands.CalculateFranchiseSeasonMetrics;
using SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonMetricsGeneration;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonById;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonMetricsById;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonMetricsBySeasonYear;
using SportsData.Producer.Application.GroupSeasons;
using SportsData.Producer.Application.Images;
using SportsData.Producer.Application.SeasonWeek.Commands.EnqueueSeasonWeekContestsUpdate;
using SportsData.Producer.Application.Services;
using SportsData.Producer.Application.Venues;
using SportsData.Producer.Application.Venues.Commands.GeocodeVenue;
using SportsData.Producer.Application.Venues.Queries.GetAllVenues;
using SportsData.Producer.Application.Venues.Queries.GetVenueById;
using SportsData.Producer.Config;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Golf;
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
                // The generic factory ensures the correct concrete DbContext type is passed to processors,
                // which enables the MassTransit outbox interceptor for transactional event publishing.
                
                return mode switch
                {
                    Sport.FootballNcaa or Sport.FootballNfl => new DocumentProcessorFactory<FootballDataContext>(
                        provider,
                        provider.GetRequiredService<ILogger<DocumentProcessorFactory<FootballDataContext>>>(),
                        provider.GetRequiredService<FootballDataContext>(),
                        provider.GetRequiredService<IDocumentProcessorRegistry>()),
                    
                    Sport.GolfPga => new DocumentProcessorFactory<GolfDataContext>(
                        provider,
                        provider.GetRequiredService<ILogger<DocumentProcessorFactory<GolfDataContext>>>(),
                        provider.GetRequiredService<GolfDataContext>(),
                        provider.GetRequiredService<IDocumentProcessorRegistry>()),
                    
                    _ => throw new NotSupportedException($"Sport mode '{mode}' is not supported for document processing")
                };
            });

            services.AddScoped<IImageProcessorFactory>(provider =>
            {
                // Sport-specific factory registration (same pattern as DocumentProcessorFactory)
                var appMode = provider.GetRequiredService<IAppMode>();
                var decoder = provider.GetRequiredService<IDecodeDocumentProvidersAndTypes>();
                
                return mode switch
                {
                    Sport.FootballNcaa or Sport.FootballNfl => new ImageProcessorFactory<FootballDataContext>(
                        appMode,
                        decoder,
                        provider,
                        provider.GetRequiredService<FootballDataContext>(),
                        provider.GetRequiredService<ILogger<ImageProcessorFactory<FootballDataContext>>>()),
                    
                    Sport.GolfPga => new ImageProcessorFactory<GolfDataContext>(
                        appMode,
                        decoder,
                        provider,
                        provider.GetRequiredService<GolfDataContext>(),
                        provider.GetRequiredService<ILogger<ImageProcessorFactory<GolfDataContext>>>()),
                    
                    _ => throw new NotSupportedException($"Sport mode '{mode}' is not supported for image processing")
                };
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

            // SeasonWeek Commands
            services.AddScoped<IEnqueueSeasonWeekContestsUpdateCommandHandler, EnqueueSeasonWeekContestsUpdateCommandHandler>();

            // Competition Commands
            services.AddScoped<IRefreshCompetitionDrivesCommandHandler, RefreshCompetitionDrivesCommandHandler>();
            services.AddScoped<ICalculateCompetitionMetricsCommandHandler, CalculateCompetitionMetricsCommandHandler>();
            services.AddScoped<IEnqueueCompetitionMetricsCalculationCommandHandler, EnqueueCompetitionMetricsCalculationCommandHandler>();
            services.AddScoped<IRefreshCompetitionMetricsCommandHandler, RefreshCompetitionMetricsCommandHandler>();
            services.AddScoped<IRefreshCompetitionMediaCommandHandler, RefreshCompetitionMediaCommandHandler>();
            services.AddScoped<IEnqueueCompetitionMediaRefreshCommandHandler, EnqueueCompetitionMediaRefreshCommandHandler>();
            services.AddScoped<IRefreshAllCompetitionMediaCommandHandler, RefreshAllCompetitionMediaCommandHandler>();

            // FranchiseSeason Commands
            services.AddScoped<IEnqueueFranchiseSeasonMetricsGenerationCommandHandler, EnqueueFranchiseSeasonMetricsGenerationCommandHandler>();
            services.AddScoped<ICalculateFranchiseSeasonMetricsCommandHandler, CalculateFranchiseSeasonMetricsCommandHandler>();

            // Franchise Queries
            services.AddScoped<IGetAllFranchisesQueryHandler, GetAllFranchisesQueryHandler>();
            services.AddScoped<IGetFranchiseByIdQueryHandler, GetFranchiseByIdQueryHandler>();
            services.AddScoped<IGetFranchiseSeasonsQueryHandler, GetFranchiseSeasonsQueryHandler>();
            services.AddScoped<IGetSeasonContestsQueryHandler, GetSeasonContestsQueryHandler>();

            // FranchiseSeason Queries
            services.AddScoped<IGetFranchiseSeasonByIdQueryHandler, GetFranchiseSeasonByIdQueryHandler>();
            services.AddScoped<IGetFranchiseSeasonMetricsByIdQueryHandler, GetFranchiseSeasonMetricsByIdQueryHandler>();
            services.AddScoped<IGetFranchiseSeasonMetricsBySeasonYearQueryHandler, GetFranchiseSeasonMetricsBySeasonYearQueryHandler>();

            // Contest Queries
            services.AddScoped<IGetContestByIdQueryHandler, GetContestByIdQueryHandler>();
            services.AddScoped<IGetContestOverviewQueryHandler, GetContestOverviewQueryHandler>();

            services.AddScoped<IGroupSeasonsService, GroupSeasonsService>();
            services.AddScoped<ILogoSelectionService, LogoSelectionService>();

            services.AddScoped<IContestReplayService, ContestReplayService>();

            services.AddScoped<IFootballCompetitionBroadcastingJob, FootballCompetitionStreamer>();

            // FranchiseSeasonRanking Queries
            services.AddScoped<IGetCurrentPollsQueryHandler, GetCurrentPollsQueryHandler>();

            // Venue Commands
            services.AddScoped<IGeocodeVenueCommandHandler, GeocodeVenueCommandHandler>();

            // Venue Queries
            services.AddScoped<IGetAllVenuesQueryHandler, GetAllVenuesQueryHandler>();
            services.AddScoped<IGetVenueByIdentifierQueryHandler, GetVenueByIdQueryHandler>();

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
