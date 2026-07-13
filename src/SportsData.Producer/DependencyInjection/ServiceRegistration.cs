using FluentValidation;

using Hangfire;

using SportsData.Core.Common;
using SportsData.Core.Config;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions;
using SportsData.Producer.Application.Competitions.Reconcile;
using SportsData.Producer.Application.Consumers;
using SportsData.Producer.Application.Competitions.Commands.CalculateCompetitionMetrics;
using SportsData.Producer.Application.Competitions.Commands.EnqueueCompetitionMediaRefresh;
using SportsData.Producer.Application.Competitions.Commands.EnqueueCompetitionMetricsCalculation;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionDrives;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMedia;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMetrics;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Application.Contests.Commands;
using SportsData.Producer.Application.Contests.Queries.GetContestById;
using SportsData.Producer.Application.Contests.Queries.GetContestOverview;
using SportsData.Producer.Application.Contests.Queries.GetContestPlayLog;
using SportsData.Producer.Application.Contests.Queries.Matchups.GetCompletedFbsContestIds;
using SportsData.Producer.Application.Contests.Queries.Matchups.GetContestResults;
using SportsData.Producer.Application.Contests.Queries.Matchups.GetFinalizedContestIds;
using SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupByContestId;
using SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupForPreview;
using SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupResult;
using SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsByContestIds;
using SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsForCurrentWeek;
using SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsForSeasonWeek;
using SportsData.Producer.Application.Documents.Commands.ReprocessDeadLetterQueue;
using SportsData.Producer.Application.Documents.Processors;
using SportsData.Producer.Application.Franchises;
using SportsData.Producer.Application.Franchises.Commands;
using SportsData.Producer.Application.Franchises.Commands.UpdateLogoDarkBg;
using SportsData.Producer.Application.Franchises.Queries.GetAllFranchises;
using SportsData.Producer.Application.Franchises.Queries.GetFranchiseById;
using SportsData.Producer.Application.Franchises.Queries.GetFranchiseLogos;
using SportsData.Producer.Application.Franchises.Queries.GetFranchiseSeasons;
using SportsData.Producer.Application.Franchises.Queries.GetSeasonContests;
using SportsData.Producer.Application.Franchises.Queries.GetTeamCard;
using SportsData.Producer.Application.Franchises.Queries.GetTeamFinalizedGames;
using SportsData.Producer.Application.Franchises.Queries.GetTeamRoster;
using SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetCurrentPolls;
using SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetPollBySeasonWeekId;
using SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetRankingsByPollByWeek;
using SportsData.Producer.Application.FranchiseSeasons.Commands.CalculateFranchiseSeasonMetrics;
using SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonEnrichment;
using SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonMetricsGeneration;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonById;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonCompetitionResults;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonMetricsById;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonMetricsBySeasonYear;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonPreviewStats;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonStatistics;
using SportsData.Producer.Application.GroupSeasons;
using SportsData.Producer.Application.GroupSeasons.Queries.GetConferenceIdsBySlugs;
using SportsData.Producer.Application.Images;
using SportsData.Producer.Application.Seasons.Queries.GetCompletedSeasonWeeks;
using SportsData.Producer.Application.Seasons.Queries.GetCurrentAndLastSeasonWeeks;
using SportsData.Producer.Application.Seasons.Queries.GetCurrentSeasonWeek;
using SportsData.Producer.Application.Seasons.Queries.GetSeasonOverview;
using SportsData.Producer.Application.Seasons.Queries.GetSeasonWeeksByDateRange;
using SportsData.Producer.Application.SeasonWeek.Commands.EnqueueSeasonWeekContestsUpdate;
using SportsData.Producer.Application.Services;
using SportsData.Producer.Application.Venues;
using SportsData.Producer.Application.Venues.Commands.GeocodeVenue;
using SportsData.Producer.Application.Venues.Queries.GetAllVenues;
using SportsData.Producer.Application.Venues.Queries.GetVenueById;
using SportsData.Producer.Config;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Golf;
using SportsData.Producer.Infrastructure.Geo;
using SportsData.Producer.Infrastructure.Sql;

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

            services.AddHttpClient("RabbitMqManagement", (sp, client) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var baseUrl = config[CommonConfigKeys.RabbitMqManagementApiBaseUrl];
                if (!string.IsNullOrWhiteSpace(baseUrl))
                    client.BaseAddress = new Uri(baseUrl);
            });
            services.AddScoped<IValidator<ReprocessDeadLetterQueueCommand>, ReprocessDeadLetterQueueCommandValidator>();
            services.AddScoped<IReprocessDeadLetterQueueCommandHandler, ReprocessDeadLetterQueueCommandHandler>();

            services.AddSingleton<ProducerSqlQueryProvider>();

            services.AddScoped<IDataContextFactory, DataContextFactory>();

            services.AddDataPersistenceExternal();

            services.AddScoped<DocumentCreatedProcessor>();
            services.AddScoped<ICompetitorScoreUpdatedConsumerHandler, CompetitorScoreUpdatedConsumerHandler>();

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

                    Sport.BaseballMlb => new DocumentProcessorFactory<BaseballDataContext>(
                        provider,
                        provider.GetRequiredService<ILogger<DocumentProcessorFactory<BaseballDataContext>>>(),
                        provider.GetRequiredService<BaseballDataContext>(),
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

                    Sport.BaseballMlb => new ImageProcessorFactory<BaseballDataContext>(
                        appMode,
                        decoder,
                        provider,
                        provider.GetRequiredService<BaseballDataContext>(),
                        provider.GetRequiredService<ILogger<ImageProcessorFactory<BaseballDataContext>>>()),

                    _ => throw new NotSupportedException($"Sport mode '{mode}' is not supported for image processing")
                };
            });

            services.AddScoped<ImageRequestedProcessor>();
            services.AddScoped<ImageProcessedProcessor>();

            services.AddScoped<IProcessImageRequests, ImageRequestedProcessor>();
            services.AddScoped<IProcessProcessedImages, ImageProcessedProcessor>();

            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();

            // ContestEnrichment is team-sport-only. Each sport has its own concrete
            // processor — football uses the play-based primary path, baseball uses
            // the canonical CompetitionCompetitorScore record. Hangfire resolves
            // the recurring trigger through IContestEnrichmentJob to avoid the
            // closed-generic type-resolution issue that previously bit ContestUpdateJob.
            switch (mode)
            {
                case Sport.FootballNcaa:
                case Sport.FootballNfl:
                    services.AddScoped<IEnrichContests, FootballContestEnrichmentProcessor>();
                    services.AddScoped<IContestEnrichmentJob, ContestEnrichmentJob<FootballDataContext>>();
                    services.AddScoped<IAuditContestEnrichment, ContestEnrichmentAuditProcessor<FootballDataContext>>();
                    services.AddScoped<IContestEnrichmentAuditJob, ContestEnrichmentAuditJob<FootballDataContext>>();
                    break;
                case Sport.BaseballMlb:
                    services.AddScoped<IEnrichContests, BaseballContestEnrichmentProcessor>();
                    services.AddScoped<IContestEnrichmentJob, ContestEnrichmentJob<BaseballDataContext>>();
                    services.AddScoped<IAuditContestEnrichment, ContestEnrichmentAuditProcessor<BaseballDataContext>>();
                    services.AddScoped<IContestEnrichmentAuditJob, ContestEnrichmentAuditJob<BaseballDataContext>>();
                    break;
            }

            services.AddScoped<IEnrichFranchiseSeasons, EnrichFranchiseSeasonHandler<TeamSportDataContext>>();
            services.AddScoped<FranchiseSeasonEnrichmentJob>();

            // ContestUpdate is team-sport-only (uses SeasonWeeks/Contests on TeamSportDataContext).
            // Golf has no team-style contests, so no registration is provided for Sport.GolfPga.
            // Register IContestUpdateJob (not the closed generic) so Hangfire can store and
            // re-resolve the recurring job by a stable, non-generic type name.
            switch (mode)
            {
                case Sport.FootballNcaa:
                case Sport.FootballNfl:
                    services.AddScoped<IUpdateContests, ContestUpdateProcessor<FootballDataContext>>();
                    services.AddScoped<IContestUpdateJob, ContestUpdateJob<FootballDataContext>>();
                    break;
                case Sport.BaseballMlb:
                    services.AddScoped<IUpdateContests, ContestUpdateProcessor<BaseballDataContext>>();
                    services.AddScoped<IContestUpdateJob, ContestUpdateJob<BaseballDataContext>>();
                    break;
            }

            // FinalizationReconcileJob: durable backstop that recovers stranded
            // CompetitionStream rows when the streamer fails to publish ContestCompleted
            // (KEDA scale-down mid-game, OOM, 5h MaxStreamDuration). Same closed-generic
            // pattern as ContestEnrichmentJob; only MLB is wired up initially per the
            // sequencing in docs/contest-finalization-reconcile-backstop.md. Football
            // joins when its Daemon Deployments ship pre-season.
            switch (mode)
            {
                case Sport.BaseballMlb:
                    services.AddScoped<IFinalizationReconcileJob, FinalizationReconcileJob<BaseballDataContext>>();
                    break;
            }

            // SeasonWeek Commands
            services.AddScoped<IEnqueueSeasonWeekContestsUpdateCommandHandler, EnqueueSeasonWeekContestsUpdateCommandHandler>();

            // Competition Commands
            services.AddScoped<IEnqueueCompetitionMetricsCalculationCommandHandler, EnqueueCompetitionMetricsCalculationCommandHandler>();
            if (mode is Sport.FootballNcaa or Sport.FootballNfl)
            {
                // Drives are football-only and the handler depends on
                // FootballDataContext (which is only registered on football
                // pods). Same constraint as the metrics handlers below.
                services.AddScoped<IRefreshCompetitionDrivesCommandHandler, RefreshCompetitionDrivesCommandHandler>();
                services.AddScoped<ICalculateCompetitionMetricsCommandHandler, CalculateCompetitionMetricsCommandHandler>();
                services.AddScoped<IRefreshCompetitionMetricsCommandHandler, RefreshCompetitionMetricsCommandHandler>();
            }
            services.AddScoped<IRefreshCompetitionMediaCommandHandler, RefreshCompetitionMediaCommandHandler>();
            services.AddScoped<IEnqueueCompetitionMediaRefreshCommandHandler, EnqueueCompetitionMediaRefreshCommandHandler>();
            services.AddScoped<IRefreshAllCompetitionMediaCommandHandler, RefreshAllCompetitionMediaCommandHandler>();

            // FranchiseSeason Commands
            services.AddScoped<IEnqueueFranchiseSeasonMetricsGenerationCommandHandler, EnqueueFranchiseSeasonMetricsGenerationCommandHandler>();
            services.AddScoped<IEnqueueFranchiseSeasonEnrichmentCommandHandler, EnqueueFranchiseSeasonEnrichmentCommandHandler>();
            if (mode is Sport.FootballNcaa or Sport.FootballNfl)
            {
                // CalculateFranchiseSeasonMetricsCommandHandler depends on FootballDataContext.
                services.AddScoped<ICalculateFranchiseSeasonMetricsCommandHandler, CalculateFranchiseSeasonMetricsCommandHandler>();
            }

            // FranchiseSeason Command Validators
            services.AddScoped<FluentValidation.IValidator<EnqueueFranchiseSeasonEnrichmentCommand>, EnqueueFranchiseSeasonEnrichmentCommandValidator>();

            // Franchise Queries
            services.AddScoped<IGetAllFranchisesQueryHandler, GetAllFranchisesQueryHandler>();
            services.AddScoped<IGetFranchiseByIdQueryHandler, GetFranchiseByIdQueryHandler>();
            services.AddScoped<IGetFranchiseSeasonsQueryHandler, GetFranchiseSeasonsQueryHandler>();
            services.AddScoped<IGetSeasonContestsQueryHandler, GetSeasonContestsQueryHandler>();
            services.AddScoped<IGetTeamCardQueryHandler, GetTeamCardQueryHandler>();
            services.AddScoped<IGetTeamFinalizedGamesQueryHandler, GetTeamFinalizedGamesQueryHandler>();
            services.AddScoped<IGetTeamRosterQueryHandler, GetTeamRosterQueryHandler>();
            services.AddScoped<IGetFranchiseLogosQueryHandler, GetFranchiseLogosQueryHandler>();
            services.AddScoped<IUpdateLogoDarkBgCommandHandler, UpdateLogoDarkBgCommandHandler>();

            // FranchiseSeason Queries
            services.AddScoped<IGetFranchiseSeasonByIdQueryHandler, GetFranchiseSeasonByIdQueryHandler>();
            services.AddScoped<IGetFranchiseSeasonMetricsByIdQueryHandler, GetFranchiseSeasonMetricsByIdQueryHandler>();
            services.AddScoped<IGetFranchiseSeasonMetricsBySeasonYearQueryHandler, GetFranchiseSeasonMetricsBySeasonYearQueryHandler>();
            services.AddScoped<IGetFranchiseSeasonStatisticsQueryHandler, GetFranchiseSeasonStatisticsQueryHandler>();
            services.AddScoped<IGetFranchiseSeasonPreviewStatsQueryHandler, GetFranchiseSeasonPreviewStatsQueryHandler>();
            services.AddScoped<IGetFranchiseSeasonCompetitionResultsQueryHandler, GetFranchiseSeasonCompetitionResultsQueryHandler>();

            // Contest Commands
            services.AddScoped<IFinalizeContestsBySeasonYearHandler, FinalizeContestsBySeasonYearHandler>();
            services.AddScoped<IReenrichContestHandler, ReenrichContestHandler>();

            // Contest Command Validators
            services.AddScoped<FluentValidation.IValidator<FinalizeContestsBySeasonYearCommand>, FinalizeContestsBySeasonYearCommandValidator>();
            services.AddScoped<FluentValidation.IValidator<ReenrichContestCommand>, ReenrichContestCommandValidator>();

            // Contest Queries
            services.AddScoped<IGetContestByIdQueryHandler, GetContestByIdQueryHandler>();
            services.AddScoped<IGetContestOverviewQueryHandler, GetContestOverviewQueryHandler>();
            services.AddScoped<IGetContestPlayLogQueryHandler, GetContestPlayLogQueryHandler>();

            // Contest Query Validators
            services.AddScoped<FluentValidation.IValidator<GetContestOverviewQuery>, GetContestOverviewQueryValidator>();
            services.AddScoped<FluentValidation.IValidator<GetContestPlayLogQuery>, GetContestPlayLogQueryValidator>();

            services.AddScoped<IGroupSeasonsService, GroupSeasonsService>();
            services.AddScoped<ILogoSelectionService, LogoSelectionService>();

            if (mode is Sport.FootballNcaa or Sport.FootballNfl)
            {
                // Depend on FootballDataContext.
                services.AddScoped<IFootballContestReplayService, FootballContestReplayService>();
                services.AddScoped<ICompetitionBroadcastingJob, FootballCompetitionStreamer>();
                services.AddScoped<CompetitionStreamScheduler>();
                services.AddScoped<IContestStartTimeUpdatedConsumerHandler, ContestStartTimeUpdatedConsumerHandler>();
            }

            if (mode is Sport.BaseballMlb)
            {
                // Depend on BaseballDataContext.
                services.AddScoped<IBaseballContestReplayService, BaseballContestReplayService>();
                services.AddScoped<ICompetitionBroadcastingJob, BaseballCompetitionStreamer>();
                services.AddScoped<CompetitionStreamScheduler>();
                services.AddScoped<IContestStartTimeUpdatedConsumerHandler, ContestStartTimeUpdatedConsumerHandler>();
            }

            // Season Queries
            services.AddScoped<IGetSeasonOverviewQueryHandler, GetSeasonOverviewQueryHandler>();
            services.AddScoped<IGetCurrentSeasonWeekQueryHandler, GetCurrentSeasonWeekQueryHandler>();
            services.AddScoped<IGetCurrentAndLastSeasonWeeksQueryHandler, GetCurrentAndLastSeasonWeeksQueryHandler>();
            services.AddScoped<IGetCompletedSeasonWeeksQueryHandler, GetCompletedSeasonWeeksQueryHandler>();
            services.AddScoped<IGetSeasonWeeksByDateRangeQueryHandler, GetSeasonWeeksByDateRangeQueryHandler>();

            // Contest Matchup Queries
            services.AddScoped<IGetMatchupsForCurrentWeekQueryHandler, GetMatchupsForCurrentWeekQueryHandler>();
            services.AddScoped<IGetMatchupsForSeasonWeekQueryHandler, GetMatchupsForSeasonWeekQueryHandler>();
            services.AddScoped<IGetMatchupByContestIdQueryHandler, GetMatchupByContestIdQueryHandler>();
            services.AddScoped<
                Application.Contests.Queries.GameDates.IGetGameDatesQueryHandler,
                Application.Contests.Queries.GameDates.GetGameDatesQueryHandler>();
            services.AddScoped<IGetMatchupsByContestIdsQueryHandler, GetMatchupsByContestIdsQueryHandler>();
            services.AddScoped<IGetMatchupForPreviewQueryHandler, GetMatchupForPreviewQueryHandler>();
            services.AddScoped<IGetMatchupResultQueryHandler, GetMatchupResultQueryHandler>();
            services.AddScoped<IGetContestResultsByContestIdsQueryHandler, GetContestResultsByContestIdsQueryHandler>();
            services.AddScoped<IGetFinalizedContestIdsQueryHandler, GetFinalizedContestIdsQueryHandler>();
            services.AddScoped<IGetCompletedFbsContestIdsQueryHandler, GetCompletedFbsContestIdsQueryHandler>();

            // FranchiseSeasonRanking Queries
            services.AddScoped<IGetCurrentPollsQueryHandler, GetCurrentPollsQueryHandler>();
            services.AddScoped<IGetPollBySeasonWeekIdQueryHandler, GetPollBySeasonWeekIdQueryHandler>();
            services.AddScoped<IGetRankingsByPollByWeekQueryHandler, GetRankingsByPollByWeekQueryHandler>();

            // GroupSeason Queries
            services.AddScoped<SportsData.Producer.Application.GroupSeasons.Queries.GetConferenceNamesAndSlugs.IGetConferenceNamesAndSlugsQueryHandler, SportsData.Producer.Application.GroupSeasons.Queries.GetConferenceNamesAndSlugs.GetConferenceNamesAndSlugsQueryHandler>();
            services.AddScoped<IGetConferenceIdsBySlugsQueryHandler, GetConferenceIdsBySlugsQueryHandler>();

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

            if (mode is Sport.FootballNcaa or Sport.FootballNfl or Sport.BaseballMlb)
            {
                recurringJobManager.AddOrUpdate<IContestEnrichmentJob>(
                    "ContestEnrichmentJob",
                    job => job.ExecuteAsync(),
                    Cron.Weekly);

                // Nightly at 06:00 UTC — after all US prime-time games have
                // ended and any post-game enrichment lag has resolved. Picks
                // up corruption like the 2026-06-18 Rockies @ Cubs case
                // (FinalizedUtc + null Winner from a stale-source race).
                recurringJobManager.AddOrUpdate<IContestEnrichmentAuditJob>(
                    "ContestEnrichmentAuditJob",
                    job => job.ExecuteAsync(),
                    "0 6 * * *");
            }

            if (mode is Sport.FootballNcaa or Sport.FootballNfl or Sport.BaseballMlb)
            {
                recurringJobManager.AddOrUpdate<IContestUpdateJob>(
                    "ContestUpdateJob",
                    job => job.ExecuteAsync(),
                    Cron.Daily);
            }

            if (mode is Sport.FootballNcaa or Sport.FootballNfl)
            {
                recurringJobManager.AddOrUpdate<FootballCompetitionMetricsAuditJob>(
                    nameof(FootballCompetitionMetricsAuditJob),
                    job => job.ExecuteAsync(),
                    "0 7 * * 0"); // Sunday at 07:00 UTC

                recurringJobManager.AddOrUpdate<CompetitionStreamScheduler>(
                    nameof(CompetitionStreamScheduler),
                    job => job.Execute(),
                    "0 7 * * 0"); // Sunday at 07:00 UTC
            }

            if (mode is Sport.BaseballMlb)
            {
                // MLB plays daily, so re-run the scheduler each morning to catch
                // newly-rolled SeasonWeek windows. The scheduler is idempotent —
                // already-scheduled streams are skipped via the existing CompetitionStream row.
                recurringJobManager.AddOrUpdate<CompetitionStreamScheduler>(
                    nameof(CompetitionStreamScheduler),
                    job => job.Execute(),
                    "0 7 * * *"); // Daily at 07:00 UTC

                // FinalizationReconcileJob — durable backstop. Every 15 minutes
                // it sweeps for CompetitionStream rows whose streamer process
                // died before publishing ContestCompleted, re-checks ESPN status,
                // and publishes the finalization events if the game is now FINAL.
                // [Queue("daemon")] on IFinalizationReconcileJob.ExecuteAsync
                // routes the trigger to the daemon queue.
                recurringJobManager.AddOrUpdate<IFinalizationReconcileJob>(
                    "FinalizationReconcileJob",
                    job => job.ExecuteAsync(CancellationToken.None),
                    "*/15 * * * *"); // Every 15 minutes
            }

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
