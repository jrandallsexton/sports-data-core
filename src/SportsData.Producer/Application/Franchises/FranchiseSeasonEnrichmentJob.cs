using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Franchises.Commands;
using SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonMetricsGeneration;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Franchises
{
    /// <summary>
    /// Weekly "make franchise seasons current" job — ALL THREE halves of
    /// that promise: per-team record enrichment (W/L from finalized
    /// contests), analytics metrics generation, and an ESPN season
    /// statistics refresh. The first two were closed 2026-09-09 (metrics
    /// were reachable only through the manual endpoint); the statistics leg
    /// followed 2026-09-10, when FAMU@Miami exposed that statistics only
    /// refreshed as a SIDE EFFECT of poll appearances — a team outside the
    /// polls kept its empty initial-sourcing statistics forever.
    /// </summary>
    public class FranchiseSeasonEnrichmentJob
    {
        private readonly ILogger<FranchiseSeasonEnrichmentJob> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IEnqueueFranchiseSeasonMetricsGenerationCommandHandler _metricsGenerationHandler;
        private readonly IAppMode _appMode;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEventBus _eventBus;

        public FranchiseSeasonEnrichmentJob(
            ILogger<FranchiseSeasonEnrichmentJob> logger,
            TeamSportDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider,
            IEnqueueFranchiseSeasonMetricsGenerationCommandHandler metricsGenerationHandler,
            IAppMode appMode,
            IDateTimeProvider dateTimeProvider,
            IEventBus eventBus)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
            _metricsGenerationHandler = metricsGenerationHandler;
            _appMode = appMode;
            _dateTimeProvider = dateTimeProvider;
            _eventBus = eventBus;
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
                .Select(x => new
                {
                    x.Id,
                    // The team's TeamSeason document — the parent whose scoped
                    // re-process spawns the statistics child below.
                    EspnRef = x.ExternalIds
                        .Where(e => e.Provider == SourceDataProvider.Espn)
                        .Select(e => new { e.SourceUrl, e.SourceUrlHash })
                        .FirstOrDefault()
                })
                .ToListAsync();

            // Off-season safety: from season rollover until the new year's
            // hierarchy is sourced, this year has no rows. Pre-#744 this
            // window was a harmless no-op; keep it that way.
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

            // ── Statistics refresh ─────────────────────────────────────────
            // FranchiseSeasonStatistic rows are written only when a team's
            // TeamSeason document is (re)processed with children allowed —
            // which before this leg happened deliberately NEVER: teams in a
            // poll got refreshed as a side effect of the rankings chain, and
            // everyone else kept whatever initial sourcing captured
            // (pre-season: empty). One scoped DocumentRequested per team
            // re-processes the TeamSeason doc and spawns ONLY the statistics
            // child (IncludeLinkedDocumentTypes — the athlete-cascade-scoping
            // vocabulary: list = only these), so no athlete/record/rank
            // fan-out rides along. Direct publish is correct here: this job
            // writes nothing through the DbContext, so there is no outbox
            // transaction to join. Never throws out of the job (same rule as
            // the metrics leg below — a missed weekly pass self-heals; a
            // Hangfire retry storm re-running the fan-outs does not).
            try
            {
                var statsCorrelationId = Guid.NewGuid();
                var requested = 0;
                var skipped = 0;

                foreach (var franchiseSeason in franchiseSeasons)
                {
                    if (franchiseSeason.EspnRef is null
                        || !Uri.TryCreate(franchiseSeason.EspnRef.SourceUrl, UriKind.Absolute, out var teamSeasonUri))
                    {
                        skipped++;
                        continue;
                    }

                    await _eventBus.Publish(new DocumentRequested(
                        Id: franchiseSeason.EspnRef.SourceUrlHash,
                        ParentId: null,
                        Uri: teamSeasonUri,
                        Ref: null,
                        Sport: _appMode.CurrentSport,
                        SeasonYear: effectiveSeasonYear,
                        DocumentType: DocumentType.TeamSeason,
                        SourceDataProvider: SourceDataProvider.Espn,
                        CorrelationId: statsCorrelationId,
                        CausationId: Guid.NewGuid(),
                        IncludeLinkedDocumentTypes: [DocumentType.TeamSeasonStatistics]));

                    requested++;
                }

                _logger.LogInformation(
                    "Season statistics refresh requested for {Requested} teams ({Skipped} without an ESPN ref). SeasonYear={SeasonYear}, Sport={Sport}, CorrelationId={CorrelationId}",
                    requested, skipped, effectiveSeasonYear, _appMode.CurrentSport, statsCorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Season statistics refresh threw for {SeasonYear} ({Sport}); enrichment fan-out already ran and is not retried.",
                    effectiveSeasonYear, _appMode.CurrentSport);
            }

            // Metrics exist for FOOTBALL only: CalculateFranchiseSeasonMetrics
            // is registered inside ServiceRegistration's football-only guard
            // (it depends on FootballDataContext). On a BaseballMlb pod the
            // enqueue itself would "succeed" and every fanned-out job would
            // then fail at Hangfire activation, forever, weekly — so the
            // sport gate lives here too, mirroring the registration guard.
            // (The statistics leg above has no such gate: the
            // TeamSeasonStatistics processor is registered for every team
            // sport including BaseballMlb.)
            if (_appMode.CurrentSport is not (Sport.FootballNcaa or Sport.FootballNfl))
            {
                _logger.LogInformation(
                    "Skipping franchise season metrics generation — no metrics pipeline for {Sport}.",
                    _appMode.CurrentSport);
                return;
            }

            // Metrics ride the same weekly cadence: the handler fans out one
            // calculation job per franchise season (ALL teams as of
            // 2026-09-10 — the FBS scoping that starved FCS teams, and both
            // sides of every FBS-vs-FCS preview, is gone), same as the
            // manual endpoint.
            //
            // The metrics half must NEVER throw out of this job: an escaped
            // exception lands after the fan-outs above, and Hangfire's
            // AutomaticRetry would re-run ALL of ExecuteAsync — re-enqueueing
            // everything on every retry while metrics still never generate.
            // A missed weekly metrics pass is a logged error and self-heals
            // next run; a retry storm is not.
            try
            {
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
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Franchise season metrics generation threw for {SeasonYear} ({Sport}); enrichment fan-out already ran and is not retried.",
                    effectiveSeasonYear, _appMode.CurrentSport);
            }
        }
    }
}
