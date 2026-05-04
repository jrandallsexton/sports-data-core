using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin.Commands.BackfillLeagueScores;
using SportsData.Api.Application.Admin.Commands.GenerateGameRecap;
using SportsData.Api.Application.Admin.Commands.GenerateLoadTest;
using SportsData.Api.Application.Admin.Commands.RefreshAiExistence;
using SportsData.Api.Application.Admin.Commands.UpsertMatchupPreview;
using SportsData.Api.Application.Admin.Queries.AuditAi;
using SportsData.Api.Application.Admin.Queries.GetAiResponse;
using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutCompetitors;
using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutDrives;
using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutMetrics;
using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutPlays;
using SportsData.Api.Application.Admin.Queries.GetMatchupPreview;
using SportsData.Api.Application.Admin.SignalRDebug;
using SportsData.Api.Application.Previews;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Application.UI.Contest.Commands.SubmitContestPredictions;
using SportsData.Api.Application.UI.Contest.Dtos;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Contests.Baseball;
using SportsData.Core.Eventing.Events.Contests.Football;
using SportsData.Core.Extensions;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Admin
{
    [ApiController]
    [Route("admin")]
    [AdminApiToken]
    public class AdminController : ApiControllerBase
    {
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IEventBus _eventBus;
        private readonly IMessageDeliveryScope _deliveryScope;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            IProvideBackgroundJobs backgroundJobProvider,
            IEventBus eventBus,
            IMessageDeliveryScope deliveryScope,
            ILogger<AdminController> logger)
        {
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
            _backgroundJobProvider = backgroundJobProvider;
            _eventBus = eventBus;
            _deliveryScope = deliveryScope;
            _logger = logger;
        }

        [HttpPost]
        [Route("generate-url-identity")]
        public Task<IActionResult> GenerateUrlIdentity([FromBody] GenerateUrlIdentityCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.Url))
            {
                return Task.FromResult<IActionResult>(BadRequest("URL cannot be empty."));
            }

            var identity = _externalRefIdentityGenerator.Generate(command.Url);

            return Task.FromResult<IActionResult>(Ok(identity));
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

        [HttpPost]
        [Route("matchup/preview/{contestId}/reset")]
        public IActionResult ResetContestPreview([FromRoute] Guid contestId)
        {
            var cmd = new GenerateMatchupPreviewsCommand
            {
                ContestId = contestId
            };
            _backgroundJobProvider.Enqueue<IGenerateMatchupPreviews>(p => p.Process(cmd));
            return Accepted(new { cmd.CorrelationId });
        }

        [HttpPost]
        [Route("matchup/preview/{contestId}")]
        public async Task<ActionResult<Guid>> UpsertContestPreview(
            [FromRoute] Guid contestId,
            [FromBody] string matchupPreview,
            [FromServices] IUpsertMatchupPreviewCommandHandler handler,
            CancellationToken cancellationToken)
        {
            var command = new UpsertMatchupPreviewCommand(matchupPreview);
            var result = await handler.ExecuteAsync(command, cancellationToken);

            if (result.IsSuccess && result.Value == contestId)
                return Created($"/admin/matchup/preview/{contestId}", new { contestId });

            if (result.IsSuccess)
                return BadRequest("The provided preview does not match the specified contest ID.");

            return result.ToActionResult();
        }

        [HttpPost]
        [Route("contest/{contestId}/score")]
        public IActionResult ScoreContest([FromRoute] Guid contestId)
        {
            var cmd = new ScoreContestCommand(contestId);
            _backgroundJobProvider.Enqueue<IScoreContests>(p => p.Process(cmd));
            return Accepted(new { cmd.CorrelationId });
        }

        [HttpPost]
        [Route("ai-refresh")]
        public IActionResult RefreshAiExistence()
        {
            var correlationId = Guid.NewGuid();
            var command = new RefreshAiExistenceCommand { CorrelationId = correlationId };
            _backgroundJobProvider.Enqueue<IRefreshAiExistenceCommandHandler>(p => p.ExecuteAsync(command, CancellationToken.None));
            return Accepted(correlationId);
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

        [HttpGet]
        [Route("matchup/preview/{contestId}")]
        public async Task<ActionResult<string>> GetAiPreview(
            [FromRoute] Guid contestId,
            [FromServices] IGetMatchupPreviewQueryHandler handler,
            CancellationToken cancellationToken)
        {
            var query = new GetMatchupPreviewQuery(contestId);
            var result = await handler.ExecuteAsync(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet]
        [Route("errors/competitions-without-competitors")]
        public async Task<ActionResult<List<CompetitionWithoutCompetitorsDto>>> GetCompetitionsWithoutCompetitors(
            [FromServices] IGetCompetitionsWithoutCompetitorsQueryHandler handler,
            CancellationToken cancellationToken)
        {
            var query = new GetCompetitionsWithoutCompetitorsQuery();
            var result = await handler.ExecuteAsync(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet]
        [Route("errors/competitions-without-plays")]
        public async Task<ActionResult<List<CompetitionWithoutPlaysDto>>> GetCompetitionsWithoutPlays(
            [FromServices] IGetCompetitionsWithoutPlaysQueryHandler handler,
            CancellationToken cancellationToken)
        {
            var query = new GetCompetitionsWithoutPlaysQuery();
            var result = await handler.ExecuteAsync(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet]
        [Route("errors/competitions-without-drives")]
        public async Task<ActionResult<List<CompetitionWithoutDrivesDto>>> GetCompetitionsWithoutDrives(
            [FromServices] IGetCompetitionsWithoutDrivesQueryHandler handler,
            CancellationToken cancellationToken)
        {
            var query = new GetCompetitionsWithoutDrivesQuery();
            var result = await handler.ExecuteAsync(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet]
        [Route("errors/competitions-without-metrics")]
        public async Task<ActionResult<List<CompetitionWithoutMetricsDto>>> GetCompetitionsWithoutMetrics(
            [FromServices] IGetCompetitionsWithoutMetricsQueryHandler handler,
            CancellationToken cancellationToken)
        {
            var query = new GetCompetitionsWithoutMetricsQuery();
            var result = await handler.ExecuteAsync(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost]
        [Route("ai-predictions/{syntheticId}")]
        public async Task<IActionResult> PostBulkPicks(
            [FromRoute] string syntheticId,
            [FromBody] List<ContestPredictionDto> predictions,
            [FromServices] ISubmitContestPredictionsCommandHandler handler,
            CancellationToken cancellationToken)
        {
            var userId = Guid.Parse(syntheticId);

            var command = new SubmitContestPredictionsCommand
            {
                UserId = userId,
                Predictions = predictions
            };

            var result = await handler.ExecuteAsync(command, cancellationToken);

            if (result.IsSuccess)
                return Created();

            return BadRequest();
        }

        [HttpPost]
        [Route("ai-test")]
        public async Task<ActionResult<string>> TestAiCommunications(
            [FromBody] GetAiResponseQuery query,
            [FromServices] IGetAiResponseQueryHandler handler,
            CancellationToken cancellationToken)
        {
            var result = await handler.ExecuteAsync(query, cancellationToken);
            return result.ToActionResult();
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

        // ─────────────────────────────────────────────────────────────
        // SignalR debug harness — see docs/signalr-debug-harness-plan.md
        //
        // These endpoints publish synthetic integration events through
        // MassTransit so the API's own consumer fans them out via
        // SignalR. The web admin page subscribes to a hardcoded sandbox
        // ContestId per sport, so debug payloads never collide with
        // real picks-page contests. Use to verify the pipeline end-to-end
        // without needing a real live game.
        // ─────────────────────────────────────────────────────────────

        [HttpPost]
        [Route("signalr-debug/contest-status")]
        public async Task<IActionResult> BroadcastDebugContestStatus(
            [FromBody] DebugContestStatusRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<Sport>(request.Sport, ignoreCase: true, out var sport))
                return BadRequest($"Unknown sport '{request.Sport}'.");

            // Explicit whitelist — Sport enum includes values (e.g.
            // BasketballNba) the debug harness has no sandbox ContestId
            // for. TryParse alone would accept them and silently fall
            // through to the Football branch.
            Guid contestId;
            switch (sport)
            {
                case Sport.BaseballMlb:
                    contestId = SignalRDebugContestIds.Baseball;
                    break;
                case Sport.FootballNcaa:
                case Sport.FootballNfl:
                    contestId = SignalRDebugContestIds.Football;
                    break;
                default:
                    return BadRequest($"Unsupported sport '{request.Sport}' for SignalR debug harness.");
            }

            var correlationId = Guid.NewGuid();

            // No DbContext write here, so bypass the MassTransit outbox and
            // publish straight to the broker. UseBusOutbox would otherwise
            // require a SaveChangesAsync to flush, which we have nothing to save.
            using (_deliveryScope.Use(DeliveryMode.Direct))
            {
                await _eventBus.Publish(new ContestStatusChanged(
                    ContestId: contestId,
                    Status: request.Status,
                    Ref: null,
                    Sport: sport,
                    SeasonYear: null,
                    CorrelationId: correlationId,
                    CausationId: CausationId.Api.SignalRDebugBroadcaster
                ), cancellationToken);
            }

            var safeStatus = request.Status?
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty);

            _logger.LogInformation(
                "SignalRDebug: published ContestStatusChanged. ContestId={ContestId}, Sport={Sport}, Status={Status}, CorrelationId={CorrelationId}",
                contestId, sport, safeStatus, correlationId);

            return Accepted(new { contestId, correlationId });
        }

        [HttpPost]
        [Route("signalr-debug/football-state")]
        public async Task<IActionResult> BroadcastDebugFootballState(
            [FromBody] DebugFootballStateRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<Sport>(request.Sport, ignoreCase: true, out var sport))
                return BadRequest($"Unknown sport '{request.Sport}'.");

            // football-state is football-only by definition — reject
            // any other sport (incl. BaseballMlb / BasketballNba) rather
            // than publishing a FootballContestStateChanged for them.
            if (sport is not (Sport.FootballNcaa or Sport.FootballNfl))
                return BadRequest($"Unsupported sport '{request.Sport}' for football-state debug endpoint.");

            var contestId = SignalRDebugContestIds.Football;
            var correlationId = Guid.NewGuid();

            using (_deliveryScope.Use(DeliveryMode.Direct))
            {
                await _eventBus.Publish(new FootballContestStateChanged(
                    ContestId: contestId,
                    Period: request.Period,
                    Clock: request.Clock,
                    AwayScore: request.AwayScore,
                    HomeScore: request.HomeScore,
                    PossessionFranchiseSeasonId: request.PossessionFranchiseSeasonId,
                    IsScoringPlay: request.IsScoringPlay,
                    BallOnYardLine: request.BallOnYardLine,
                    Ref: null,
                    Sport: sport,
                    SeasonYear: null,
                    CorrelationId: correlationId,
                    CausationId: CausationId.Api.SignalRDebugBroadcaster
                ), cancellationToken);
            }

            _logger.LogInformation(
                "SignalRDebug: published FootballContestStateChanged. ContestId={ContestId}, Period={Period}, Clock={Clock}, Score={Away}-{Home}, Yard={Yard}, Scoring={Scoring}, CorrelationId={CorrelationId}",
                contestId, request.Period, request.Clock, request.AwayScore, request.HomeScore, request.BallOnYardLine, request.IsScoringPlay, correlationId);

            return Accepted(new { contestId, correlationId });
        }

        [HttpPost]
        [Route("signalr-debug/play-completed")]
        public async Task<IActionResult> BroadcastDebugContestPlayCompleted(
            [FromBody] DebugContestPlayCompletedRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<Sport>(request.Sport, ignoreCase: true, out var sport))
                return BadRequest($"Unknown sport '{request.Sport}'.");

            // Same whitelist as contest-status — reject sports the debug
            // harness has no sandbox ContestId for.
            Guid contestId;
            switch (sport)
            {
                case Sport.BaseballMlb:
                    contestId = SignalRDebugContestIds.Baseball;
                    break;
                case Sport.FootballNcaa:
                case Sport.FootballNfl:
                    contestId = SignalRDebugContestIds.Football;
                    break;
                default:
                    return BadRequest($"Unsupported sport '{request.Sport}' for SignalR debug harness.");
            }

            var correlationId = Guid.NewGuid();

            using (_deliveryScope.Use(DeliveryMode.Direct))
            {
                await _eventBus.Publish(new ContestPlayCompleted(
                    ContestId: contestId,
                    CompetitionId: contestId, // sandbox: reuse contestId so consumers don't need a real competition row
                    PlayId: Guid.NewGuid(),
                    PlayDescription: request.PlayDescription,
                    Ref: null,
                    Sport: sport,
                    SeasonYear: null,
                    CorrelationId: correlationId,
                    CausationId: CausationId.Api.SignalRDebugBroadcaster
                ), cancellationToken);
            }

            _logger.LogInformation(
                "SignalRDebug: published ContestPlayCompleted. ContestId={ContestId}, Sport={Sport}, Description={Description}, CorrelationId={CorrelationId}",
                contestId, sport, request.PlayDescription, correlationId);

            return Accepted(new { contestId, correlationId });
        }

        [HttpPost]
        [Route("signalr-debug/baseball-state")]
        public async Task<IActionResult> BroadcastDebugBaseballState(
            [FromBody] DebugBaseballStateRequest request,
            CancellationToken cancellationToken)
        {
            var contestId = SignalRDebugContestIds.Baseball;
            var correlationId = Guid.NewGuid();

            using (_deliveryScope.Use(DeliveryMode.Direct))
            {
                await _eventBus.Publish(new BaseballContestStateChanged(
                    ContestId: contestId,
                    Inning: request.Inning,
                    HalfInning: request.HalfInning,
                    AwayScore: request.AwayScore,
                    HomeScore: request.HomeScore,
                    Balls: request.Balls,
                    Strikes: request.Strikes,
                    Outs: request.Outs,
                    RunnerOnFirst: request.RunnerOnFirst,
                    RunnerOnSecond: request.RunnerOnSecond,
                    RunnerOnThird: request.RunnerOnThird,
                    AtBatAthleteId: request.AtBatAthleteId,
                    PitchingAthleteId: request.PitchingAthleteId,
                    Ref: null,
                    Sport: Sport.BaseballMlb,
                    SeasonYear: null,
                    CorrelationId: correlationId,
                    CausationId: CausationId.Api.SignalRDebugBroadcaster
                ), cancellationToken);
            }

            _logger.LogInformation(
                "SignalRDebug: published BaseballContestStateChanged. ContestId={ContestId}, Inning={Half} {Inning}, Score={Away}-{Home}, Count={Balls}-{Strikes}, Outs={Outs}, CorrelationId={CorrelationId}",
                contestId, request.HalfInning, request.Inning, request.AwayScore, request.HomeScore, request.Balls, request.Strikes, request.Outs, correlationId);

            return Accepted(new { contestId, correlationId });
        }

        /// <summary>
        /// Generates synthetic load to test KEDA autoscaling.
        /// Publishes events to RabbitMQ which are consumed and enqueued to Hangfire.
        /// KEDA monitors Hangfire queue depth and autoscales pods accordingly.
        /// </summary>
        /// <param name="command">Load test configuration</param>
        /// <param name="handler">Command handler</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Test execution details</returns>
        [HttpPost]
        [Route("keda/load-test")]
        public async Task<ActionResult<GenerateLoadTestResult>> GenerateLoadTest(
            [FromBody] GenerateLoadTestCommand command,
            [FromServices] IGenerateLoadTestCommandHandler handler,
            CancellationToken cancellationToken)
        {
            var result = await handler.ExecuteAsync(command, cancellationToken);
            return result.ToActionResult();
        }
    }
}
