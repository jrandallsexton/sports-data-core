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
            // Season-year default is SPORT-AWARE (Vortex, PR #744): football
            // seasons are labeled by their starting year and roll over in
            // June — the old calendar-year default meant every January-May
            // run targeted a not-yet-existent season while bowls/playoffs
            // still needed enriching. Baseball is the opposite: an MLB
            // season is current from opening day and labeled by the
            // calendar year, so the June rollover would spend April/May
            // enriching LAST season. Football gets the rollover; everything
            // else keeps the calendar year it always had.
            var now = _dateTimeProvider.UtcNow();
            var isFootball = _appMode.CurrentSport is Sport.FootballNcaa or Sport.FootballNfl;
            var effectiveSeasonYear = seasonYear
                ?? (isFootball && now.Month < 6 ? now.Year - 1 : now.Year);

            var franchiseSeasons = await _dataContext.FranchiseSeasons
                .AsNoTracking()
                .Where(x => x.SeasonYear == effectiveSeasonYear)
                .ToListAsync();

            // Off-season safety: from season rollover until the new year's
            // hierarchy is sourced, this year has no rows — and the NCAA
            // metrics handler THROWS on a missing FBS root
            // (GroupSeasonsService "FBS group root(s) not found") rather
            // than scoping empty. Pre-PR this window was a harmless no-op;
            // keep it that way.
            if (franchiseSeasons.Count == 0)
            {
                _logger.LogInformation(
                    "No franchise seasons exist for {SeasonYear} ({Sport}) — season not sourced yet; nothing to enrich.",
                    effectiveSeasonYear, _appMode.CurrentSport);
                return;
            }

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

            // Metrics exist for FOOTBALL only: CalculateFranchiseSeasonMetrics
            // is registered inside ServiceRegistration's football-only guard
            // (it depends on FootballDataContext). On a BaseballMlb pod the
            // enqueue itself would "succeed" and every fanned-out job would
            // then fail at Hangfire activation, forever, weekly — so the
            // sport gate lives here too, mirroring the registration guard.
            if (_appMode.CurrentSport is not (Sport.FootballNcaa or Sport.FootballNfl))
            {
                _logger.LogInformation(
                    "Skipping franchise season metrics generation — no metrics pipeline for {Sport}.",
                    _appMode.CurrentSport);
                return;
            }

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
