using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin.Commands.BackfillLeagueScores;
using SportsData.Api.Application.Admin.Commands.ReenrichContest;
using SportsData.Api.Application.Admin.Queries.AuditAi;
using SportsData.Api.Application.Admin.Queries.GetLeagueWeekContests;
using SportsData.Api.Application.Admin.Queries.GetMatchupForContest;
using SportsData.Api.Application.Contests.Commands.GenerateGameRecap;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Application.Scoring.Jobs.PickScoring;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Eventing.Events.Contests.Baseball;
using SportsData.Core.Eventing.Events.Contests.Football;
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
        [Route("contest/{contestId}/score")]
        public IActionResult ScoreContest([FromRoute] Guid contestId)
        {
            var cmd = new ScorePicksCommand(contestId);
            _backgroundJobProvider.Enqueue<IScorePicks>(p => p.Process(cmd));
            return Accepted(new { cmd.CorrelationId });
        }

        /// <summary>
        /// Admin re-enrichment path. Clears UserPick scoring fields for this
        /// contest, then asks Producer to clear the derived/enriched fields on
        /// its Contest row and re-invoke the enrichment processor synchronously.
        /// Returns the CorrelationId Producer logged the work under so the
        /// admin UI can surface it for Seq tracing.
        ///
        /// Manual recovery path for stuck WinnerFranchiseSeasonId /
        /// SpreadWinnerFranchiseSeasonId — the case where the canonical
        /// CompetitionCompetitorScores are correct but the derived fields are
        /// wrong and the nightly audit hasn't reset them yet.
        ///
        /// Lives on the admin controller (AdminApiToken-gated) deliberately:
        /// the operation rolls back UserPicks, so exposing it under the
        /// general /ui/contest surface would let a logged-in non-admin
        /// fire it (the UI's isAdmin check is presentational only — not a
        /// trust boundary).
        /// </summary>
        [HttpPost]
        [Route("contest/{contestId}/reenrich")]
        public async Task<ActionResult<Guid>> ReenrichContest(
            [FromRoute] Guid contestId,
            [FromQuery] string sport = "football",
            [FromQuery] string league = "ncaa",
            [FromServices] IReenrichContestCommandHandler handler = default!,
            CancellationToken cancellationToken = default)
        {
            var mode = ModeMapper.ResolveMode(sport, league);
            var command = new ReenrichContestCommand { ContestId = contestId, Sport = mode };
            var result = await handler.ExecuteAsync(command, cancellationToken);
            return result.ToActionResult();
        }

        /// <summary>
        /// Admin "re-source every contest for a (sport, season)". Fans out the
        /// narrowed Contest Refresh per contest through the matching per-sport
        /// Producer to backfill point-in-time records / play data (no athletes).
        /// Producer enqueues the work and returns 202.
        ///
        /// This is the front door for the by-season driver: Producer's own
        /// <c>/api/contests/refresh</c> endpoint is not internet-facing, so
        /// operator access routes through here (AdminApiToken-gated) and over to
        /// the correct Producer pod via the contest client factory. See
        /// docs/features/season-contest-resource-driver.md.
        ///
        /// Example: POST /admin/contests/refresh?sport=baseball&amp;league=mlb&amp;seasonYear=2026
        /// </summary>
        [HttpPost]
        [Route("contests/refresh")]
        public async Task<IActionResult> RefreshContestsBySeasonYear(
            [FromQuery] int seasonYear,
            [FromServices] IContestClientFactory contestClientFactory,
            [FromQuery] string sport = "football",
            [FromQuery] string league = "ncaa",
            CancellationToken cancellationToken = default)
        {
            // API runs in Sport.All; sport+league → a concrete Sport so the
            // client factory routes to the matching per-sport Producer pod.
            var mode = ModeMapper.ResolveMode(sport, league);
            var correlationId = ActivityExtensions.GetCorrelationId();

            _logger.LogInformation(
                "RefreshContestsBySeasonYear requested. Sport={Sport}, SeasonYear={SeasonYear}, CorrelationId={CorrelationId}",
                mode, seasonYear, correlationId);

            var result = await contestClientFactory
                .Resolve(mode)
                .RefreshContestsBySeasonYear(mode, seasonYear, correlationId, cancellationToken);

            return result.ToActionResult(_ =>
                Accepted(new { correlationId, sport = mode.ToString(), seasonYear }));
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
        /// Returns one canonical MLB matchup in the same shape as the picks page
        /// (<see cref="LeagueWeekMatchupsDto.MatchupForPickDto"/>), without league
        /// context. Backs the SignalR debug page so a real MatchupCard can be
        /// rendered against a chosen contest. League-context fields (Predictions,
        /// AiWinner, IsPreview*, HeadLine) are intentionally null/empty.
        /// </summary>
        [HttpGet]
        [Route("baseball/contests/{contestId:guid}/matchup")]
        public async Task<ActionResult<LeagueWeekMatchupsDto.MatchupForPickDto>> GetBaseballMatchupForContest(
            [FromRoute] Guid contestId,
            [FromServices] IGetMatchupForContestQueryHandler handler,
            CancellationToken cancellationToken)
        {
            var query = new GetMatchupForContestQuery(contestId, Sport.BaseballMlb);
            var result = await handler.ExecuteAsync(query, cancellationToken);
            return result.ToActionResult();
        }

        /// <summary>
        /// Triggers a contest replay through the matching sport's Producer.
        /// Producer enqueues the replay and the bus emits ContestStatusChanged
        /// once + a sport-specific *PlayCompleted per stored play, exercising
        /// the same SignalR fan-out path as a live game. Use the admin baseball
        /// debug page to observe the resulting events on a real <MatchupCard />.
        /// </summary>
        [HttpPost]
        [Route("baseball/contests/{contestId:guid}/replay")]
        public async Task<ActionResult<bool>> ReplayBaseballContest(
            [FromRoute] Guid contestId,
            [FromServices] IContestClientFactory contestClientFactory,
            CancellationToken cancellationToken)
        {
            var result = await contestClientFactory
                .Resolve(Sport.BaseballMlb)
                .ReplayContest(contestId, cancellationToken);

            return result.ToActionResult();
        }

        /// <summary>
        /// Football twin of <see cref="GetBaseballMatchupForContest"/>. Returns one
        /// canonical NCAA/NFL matchup in the same shape as the picks page so the
        /// football SignalR debug page can render a real MatchupCard for a chosen
        /// contest. League-context fields (Predictions, AiWinner, IsPreview*,
        /// HeadLine) are intentionally null/empty.
        /// </summary>
        [HttpGet]
        [Route("football/contests/{contestId:guid}/matchup")]
        public async Task<ActionResult<LeagueWeekMatchupsDto.MatchupForPickDto>> GetFootballMatchupForContest(
            [FromRoute] Guid contestId,
            [FromQuery] string league = "ncaa",
            [FromServices] IGetMatchupForContestQueryHandler handler = default!,
            CancellationToken cancellationToken = default)
        {
            // API runs in Sport.All; route encodes the sport (football), the
            // league query param distinguishes NCAA vs NFL. ModeMapper is the
            // canonical place for sport+league → Sport resolution.
            var sport = ModeMapper.ResolveMode("football", league);

            var query = new GetMatchupForContestQuery(contestId, sport);
            var result = await handler.ExecuteAsync(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost]
        [Route("football/contests/{contestId:guid}/replay")]
        public async Task<ActionResult<bool>> ReplayFootballContest(
            [FromRoute] Guid contestId,
            [FromQuery] string league = "ncaa",
            [FromServices] IContestClientFactory contestClientFactory = default!,
            CancellationToken cancellationToken = default)
        {
            // API runs in Sport.All; route encodes the sport (football), the
            // league query param distinguishes NCAA vs NFL. Football's two
            // leagues share the FootballDataContext so either resolves the
            // same Producer pod, but we still need a concrete Sport enum to
            // route the client factory.
            var sport = ModeMapper.ResolveMode("football", league);

            var result = await contestClientFactory
                .Resolve(sport)
                .ReplayContest(contestId, cancellationToken);

            return result.ToActionResult();
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
