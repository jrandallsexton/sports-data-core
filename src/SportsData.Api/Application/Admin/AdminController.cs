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
using SportsData.Api.Application.Previews;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Application.UI.Contest.Commands.SubmitContestPredictions;
using SportsData.Api.Application.UI.Contest.Dtos;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
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
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            IProvideBackgroundJobs backgroundJobProvider,
            ILogger<AdminController> logger)
        {
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
            _backgroundJobProvider = backgroundJobProvider;
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

        /// <summary>
        /// Generates synthetic load to test KEDA autoscaling.
        /// Publishes events to RabbitMQ which are consumed and enqueued to Hangfire.
        /// </summary>
        /// <param name="count">Number of test jobs to create (default: 50)</param>
        /// <param name="target">Target service: 'producer', 'provider', or 'both' (default: 'both')</param>
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
