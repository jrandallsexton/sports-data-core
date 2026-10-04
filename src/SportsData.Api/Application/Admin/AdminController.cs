using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin.Commands.BackfillLeagueScores;
using SportsData.Api.Application.Admin.Queries.AuditAi;
using SportsData.Api.Application.Admin.Queries.GetLeagueWeekContests;
using SportsData.Api.Application.Contests.Commands.GenerateGameRecap;
using SportsData.Api.Application.Scoring;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Infrastructure.Clients.MetricBot;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Admin
{
    [ApiController]
    [Route("admin")]
    [AdminApiToken]
    public class AdminController : ApiControllerBase
    {
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IProvideBackgroundJobs backgroundJobProvider,
            ILogger<AdminController> logger)
        {
            _backgroundJobProvider = backgroundJobProvider;
            _logger = logger;
        }

        /// <summary>
        /// Test game recap generation with large prompt + JSON data
        /// Example: POST /admin/ai/game-recap
        /// Body: { "gameDataJson": "{ ... your large JSON ... }", "reloadPrompt": false }
        /// </summary>
        [HttpPost]
        [Route("ai/game-recap")]
        public async Task<ActionResult<GameRecapResponse>> GenerateGameRecap(
            [FromBody] GenerateGameRecapCommand command,
            [FromServices] IGenerateGameRecapCommandHandler handler,
            CancellationToken cancellationToken)
        {
            var result = await handler.ExecuteAsync(command, cancellationToken);
            return result.ToActionResult();
        }

        /// <summary>
        /// Fan out ESPN sourcing for every FranchiseSeason in a season year,
        /// optionally narrowed to specific child document types. Producer is
        /// not publicly reachable; this proxy is the operator's entry point.
        ///
        /// Example: POST /admin/sourcing/franchise-seasons/FootballNcaa/2025
        /// Body: { "includeLinkedDocumentTypes": ["TeamSeasonRecord"] }
        /// (empty body object {} = the historical full cascade)
        /// </summary>
        [HttpPost]
        [Route("sourcing/franchise-seasons/{sport}/{seasonYear:int}")]
        public async Task<ActionResult<Guid>> RequestFranchiseSeasonSourcing(
            [FromRoute] Sport sport,
            [FromRoute] int seasonYear,
            [FromBody] FranchiseSeasonSourcingRequest request,
            [FromServices] IFranchiseClientFactory franchiseClientFactory,
            CancellationToken cancellationToken)
        {
            var client = franchiseClientFactory.Resolve(sport);
            var result = await client.RequestFranchiseSeasonSourcing(
                seasonYear, request, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost]
        [Route("ai-audit")]
        public IActionResult AiPreviewsAudit()
        {
            var correlationId = Guid.NewGuid();
            var query = new AuditAiQuery { CorrelationId = correlationId };
            _backgroundJobProvider.Enqueue<IAuditAiQueryHandler>(p => p.ExecuteAsync(query, CancellationToken.None));
            return Accepted(correlationId);
        }

        /// <summary>
        /// Replays every Final contest on a pickem league's matchup page
        /// for a given week, exercising the Producer → broker → API →
        /// SignalR fan-out path against every MatchupCard the user sees
        /// at GET /ui/leagues/{leagueId}/matchups/{week}. Used to verify
        /// that each card updates independently when its contest's plays
        /// arrive over SignalR — the multi-card analogue of the per-sport
        /// admin debug pages.
        ///
        /// Filtered to Status=="Final" because the per-contest replay
        /// service emits ContestStatusChanged=InProgress as its first
        /// step (no "revert to Scheduled" counterpart). Replaying a
        /// scheduled game would briefly flash it to In Progress in the
        /// UI with no recovery — undesired noise during testing.
        /// </summary>
        [HttpPost]
        [Route("leagues/{leagueId:guid}/weeks/{week:int}/replay")]
        public async Task<IActionResult> ReplayLeagueWeekContests(
            [FromRoute] Guid leagueId,
            [FromRoute] int week,
            [FromServices] IGetLeagueWeekContestsQueryHandler queryHandler,
            [FromServices] IContestClientFactory contestClientFactory,
            CancellationToken cancellationToken)
        {
            var queryResult = await queryHandler.ExecuteAsync(
                new GetLeagueWeekContestsQuery(leagueId, week),
                cancellationToken);

            if (!queryResult.IsSuccess)
                return queryResult.ToActionResult().Result!;

            var (sport, contestIds) = queryResult.Value;
            if (contestIds.Count == 0)
            {
                return Accepted(new
                {
                    leagueId,
                    week,
                    sport = sport.ToString(),
                    totalMatchups = 0,
                    finalContests = 0,
                    replaysQueued = 0,
                });
            }

            var client = contestClientFactory.Resolve(sport);

            // Canonical statuses come from Producer in one round-trip;
            // .ToList() is for the cast — GetMatchupsByContestIds takes
            // a List<Guid>, contestIds is IReadOnlyList<Guid>.
            // Direction = Roundel: admin path, no user preference context.
            var matchupsResult = await client.GetMatchupsByContestIds(
                contestIds.ToList(),
                MarkDirection.Roundel,
                cancellationToken);

            if (!matchupsResult.IsSuccess)
                return matchupsResult.ToActionResult().Result!;

            var finalContestIds = (matchupsResult.Value ?? new List<LeagueMatchupDto>())
                .Where(m => string.Equals(m.Status, "Final", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.ContestId)
                .ToList();

            if (finalContestIds.Count == 0)
            {
                _logger.LogInformation(
                    "ReplayLeagueWeek: no Final contests to replay. LeagueId={LeagueId}, Week={Week}, Sport={Sport}, TotalMatchups={TotalMatchups}",
                    leagueId, week, sport, contestIds.Count);
                return Accepted(new
                {
                    leagueId,
                    week,
                    sport = sport.ToString(),
                    totalMatchups = contestIds.Count,
                    finalContests = 0,
                    replaysQueued = 0,
                });
            }

            // Fan out per-contest replays in parallel. Producer enqueues
            // a Hangfire job per call and returns immediately, so this is
            // a quick burst of HTTP calls — not waiting on play emission.
            var replayResults = await Task.WhenAll(
                finalContestIds.Select(id => client.ReplayContest(id, cancellationToken)));

            var succeeded = replayResults.Count(r => r.IsSuccess);
            var failed = replayResults.Length - succeeded;

            _logger.LogInformation(
                "ReplayLeagueWeek: queued. LeagueId={LeagueId}, Week={Week}, Sport={Sport}, TotalMatchups={TotalMatchups}, FinalContests={FinalContests}, Succeeded={Succeeded}, Failed={Failed}",
                leagueId, week, sport, contestIds.Count, finalContestIds.Count, succeeded, failed);

            return Accepted(new
            {
                leagueId,
                week,
                sport = sport.ToString(),
                totalMatchups = contestIds.Count,
                finalContests = finalContestIds.Count,
                replaysQueued = succeeded,
                replaysFailed = failed,
            });
        }

        /// <summary>
        /// Backfills league week scores for an entire season.
        /// Processes all completed weeks for the specified season year.
        /// </summary>
        /// <param name="seasonYear">The season year to backfill (e.g., 2024, 2025)</param>
        /// <returns>Summary of backfill operation</returns>
        [HttpPost]
        [Route("backfill-league-scores/{seasonYear}")]
        public async Task<ActionResult<BackfillLeagueScoresResult>> BackfillLeagueScores(
            int seasonYear,
            [FromServices] IBackfillLeagueScoresCommandHandler handler,
            CancellationToken cancellationToken)
        {
            var command = new BackfillLeagueScoresCommand(seasonYear);
            var result = await handler.ExecuteAsync(command, cancellationToken);
            return result.ToActionResult();
        }

        /// <summary>
        /// Computes the simulated $1 bet columns (PointsSU, PointsATS, PointsOU)
        /// on every already-scored UserPick from its contest's finalized result
        /// and its league matchup's prices: enqueues one background job per
        /// distinct contest. IsCorrect and PointsAwarded are not touched.
        /// Returns 202 with the correlation id and per-sport job counts.
        /// Idempotent, safe to re-run.
        /// Example: POST /admin/backfill-user-pick-bet-points
        /// </summary>
        [HttpPost]
        [Route("backfill-user-pick-bet-points")]
        public async Task<ActionResult<Application.Admin.Commands.BackfillUserPickBetPoints.BackfillUserPickBetPointsResult>> BackfillUserPickBetPoints(
            [FromServices] Application.Admin.Commands.BackfillUserPickBetPoints.IBackfillUserPickBetPointsCommandHandler handler,
            CancellationToken cancellationToken)
        {
            var result = await handler.ExecuteAsync(
                new Application.Admin.Commands.BackfillUserPickBetPoints.BackfillUserPickBetPointsCommand(),
                cancellationToken);
            return result.ToActionResult();
        }

    }
}
