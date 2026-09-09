using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Franchises.Commands;
using SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonMetricsGeneration;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Franchises
{
    /// <summary>
    /// Weekly "make franchise seasons current" job — BOTH halves of that
    /// promise: per-team record enrichment (W/L from finalized contests)
    /// AND analytics metrics generation. Metrics were previously reachable
    /// only through the manual metrics/generate endpoint, which made this
    /// job's name a lie an operator had to discover the hard way
    /// (2026-09-09: enrichment triggered for week 2, War Room metrics
    /// stayed empty).
    /// </summary>
    public class FranchiseSeasonEnrichmentJob
    {
        private readonly ILogger<FranchiseSeasonEnrichmentJob> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IEnqueueFranchiseSeasonMetricsGenerationCommandHandler _metricsGenerationHandler;
        private readonly IAppMode _appMode;
        private readonly IDateTimeProvider _dateTimeProvider;

        public FranchiseSeasonEnrichmentJob(
            ILogger<FranchiseSeasonEnrichmentJob> logger,
            TeamSportDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider,
            IEnqueueFranchiseSeasonMetricsGenerationCommandHandler metricsGenerationHandler,
            IAppMode appMode,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
            _metricsGenerationHandler = metricsGenerationHandler;
            _appMode = appMode;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task ExecuteAsync(int? seasonYear = null)
        {
            // Season-year convention: a season is labeled by its starting
            // year, rolling over in June. The old default (calendar year)
            // targeted a season that didn't exist yet every January-May run
            // — bowls/playoffs finalized in January enrich the PRIOR label.
            var now = _dateTimeProvider.UtcNow();
            var effectiveSeasonYear = seasonYear ?? (now.Month >= 6 ? now.Year : now.Year - 1);

            var franchiseSeasons = await _dataContext.FranchiseSeasons
                .AsNoTracking()
                .Where(x => x.SeasonYear == effectiveSeasonYear)
                .ToListAsync();

            _logger.LogInformation("Requesting enrichment for {count} franchise seasons for year {seasonYear}.", franchiseSeasons.Count, effectiveSeasonYear);

            foreach (var franchiseSeason in franchiseSeasons)
            {
                var cmd = new EnrichFranchiseSeasonCommand(
                    franchiseSeason.Id,
                    effectiveSeasonYear,
                    Guid.NewGuid());

                _backgroundJobProvider
                    .Enqueue<EnrichFranchiseSeasonHandler<TeamSportDataContext>>(p => p.Process(cmd));
            }

            _logger.LogInformation("All franchise season enrichment requests sent.");

            // Metrics ride the same weekly cadence: the handler applies its
            // own scoping (FBS-only for NCAA) and fans out one calculation
            // job per franchise season, same as the manual endpoint. Runs
            // regardless of the enrichment fan-out above — the enqueued
            // record updates and the metric calculations are independent
            // reads of the same finalized contests.
            var metricsResult = await _metricsGenerationHandler.ExecuteAsync(
                new EnqueueFranchiseSeasonMetricsGenerationCommand(
                    effectiveSeasonYear,
                    _appMode.CurrentSport));

            if (metricsResult.IsSuccess)
            {
                _logger.LogInformation(
                    "Franchise season metrics generation enqueued for {SeasonYear} ({Sport}).",
                    effectiveSeasonYear, _appMode.CurrentSport);
            }
            else
            {
                _logger.LogError(
                    "Franchise season metrics generation FAILED to enqueue for {SeasonYear} ({Sport}).",
                    effectiveSeasonYear, _appMode.CurrentSport);
            }
        }
    }
}
