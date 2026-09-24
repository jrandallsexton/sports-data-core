using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions.Commands.CalculateCompetitionMetrics;
using SportsData.Producer.Application.FranchiseSeasons.Commands.CalculateFranchiseSeasonMetrics;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Competitions
{
    /// <summary>
    /// Keeps per-game metrics current for football competitions that have
    /// been played. Two scopes:
    /// <list type="number">
    ///   <item><b>No rows yet</b> (the original scope): the game is at least
    ///     three hours old and has no CompetitionMetric rows.</item>
    ///   <item><b>Computed before the plays arrived</b>: the rows exist but
    ///     carry a null InputsHash, which the calculator writes only when it
    ///     ran over zero plays, and the competition now has plays. Found
    ///     2026-09-24: 139 games this season, every one computed by this
    ///     job's Sunday 07:00 UTC run hours before their play-by-play landed
    ///     in a Sunday-afternoon batch, then never revisited because "rows
    ///     exist" read as "done". Their teams' season metrics were all zeros
    ///     and the preview model saw Northwestern's zeros beside Indiana's
    ///     real numbers.</item>
    /// </list>
    /// After enqueuing the per-game recomputes, the season aggregates for
    /// every affected franchise season are scheduled to recompute a few
    /// minutes later; the aggregate is a plain average of the per-game rows,
    /// so a fixed game leaves a stale season row until then. Both handlers
    /// are idempotent (delete+insert in one save), so re-running is free.
    /// Runs daily (was weekly): a Saturday game whose plays land on Sunday
    /// afternoon must be right by Monday, before that week's previews.
    /// </summary>
    public class FootballCompetitionMetricsAuditJob : IAmARecurringJob
    {
        /// <summary>
        /// Head start for the per-game recomputes before the season
        /// aggregates read them. Dozens of jobs across 30 workers finish in
        /// seconds; the margin covers a busy queue.
        /// </summary>
        internal static readonly TimeSpan SeasonRecomputeDelay = TimeSpan.FromMinutes(10);

        private readonly ILogger<FootballCompetitionMetricsAuditJob> _logger;
        // Football-only job (registered under the football guard) that reads
        // plays, which live on the football context. It publishes nothing, so
        // the outbox-instance concern behind the abstract registrations does
        // not apply; the concrete type is simply the one with the DbSet.
        private readonly FootballDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IDateTimeProvider _dateTimeProvider;

        public FootballCompetitionMetricsAuditJob(
            ILogger<FootballCompetitionMetricsAuditJob> logger,
            FootballDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task ExecuteAsync()
        {
            var cutoff = _dateTimeProvider.UtcNow().AddHours(-3);

            var withoutMetrics = await _dataContext.Competitions
                .AsNoTracking()
                .Where(x => x.Date < cutoff && !x.Metrics.Any())
                .Select(x => new { x.Id, x.ContestId })
                .ToListAsync();

            // Rows written from zero plays (null InputsHash is the calculator's
            // marker for that) whose competition has plays now.
            var computedBeforePlays = await _dataContext.Competitions
                .AsNoTracking()
                .Where(x => x.Date < cutoff
                            && x.Metrics.Any(m => m.InputsHash == null || m.InputsHash == "")
                            && x.Plays.Any())
                .Select(x => new { x.Id, x.ContestId })
                .ToListAsync();

            var targets = withoutMetrics
                .Concat(computedBeforePlays)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList();

            if (targets.Count == 0)
            {
                _logger.LogInformation("All football competitions have current metrics.");
                return;
            }

            _logger.LogInformation(
                "Football metrics audit: {NoRows} competition(s) without rows, {StaleRows} computed before their plays arrived; recomputing {Total}.",
                withoutMetrics.Count, computedBeforePlays.Count, targets.Count);

            foreach (var target in targets)
            {
                var command = new CalculateCompetitionMetricsCommand(target.Id);
                _backgroundJobProvider.Enqueue<ICalculateCompetitionMetricsCommandHandler>(
                    h => h.ExecuteAsync(command, CancellationToken.None));
            }

            // Season aggregates for every team in a recomputed game, once.
            var contestIds = targets.Select(t => t.ContestId).Distinct().ToList();
            var teams = await _dataContext.Contests
                .AsNoTracking()
                .Where(c => contestIds.Contains(c.Id))
                .Select(c => new
                {
                    c.SeasonYear,
                    Ids = new[] { c.AwayTeamFranchiseSeasonId, c.HomeTeamFranchiseSeasonId }
                })
                .ToListAsync();

            var seasons = teams
                .SelectMany(t => t.Ids.Select(id => (FranchiseSeasonId: id, t.SeasonYear)))
                .Distinct()
                .ToList();

            foreach ((Guid franchiseSeasonId, int seasonYear) in seasons)
            {
                var recompute = new CalculateFranchiseSeasonMetricsCommand(franchiseSeasonId, seasonYear);
                _backgroundJobProvider.Schedule<ICalculateFranchiseSeasonMetricsCommandHandler>(
                    h => h.ExecuteAsync(recompute, CancellationToken.None),
                    SeasonRecomputeDelay);
            }

            _logger.LogInformation(
                "Football metrics audit: {Games} per-game recompute(s) enqueued, {Seasons} season aggregate(s) scheduled in {Delay}.",
                targets.Count, seasons.Count, SeasonRecomputeDelay);
        }
    }
}
