using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin.Commands.BackfillLeagueScores;
using SportsData.Api.Application.Admin.Commands.GenerateGameRecap;
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

        /// <summary>
        /// Initializes a new instance of AdminController with dependencies required for generating external identities, enqueuing background jobs, and logging.
        /// </summary>
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
        /// <summary>
        /// Generates an AI-produced game recap based on the supplied command.
        /// </summary>
        /// <param name="command">Parameters identifying the game and options used to produce the recap.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>An ActionResult containing the generated GameRecapResponse on success, or an appropriate error ActionResult otherwise.</returns>
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
        /// Enqueues generation of matchup previews for the specified contest.
        /// </summary>
        /// <param name="contestId">Identifier of the contest whose matchup previews should be generated.</param>
        /// <returns>202 Accepted response containing an object with the command's CorrelationId.</returns>
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

        /// <summary>
        /// Upserts a matchup preview for the specified contest.
        /// </summary>
        /// <param name="contestId">The contest identifier the preview should be associated with.</param>
        /// <param name="matchupPreview">The preview content to upsert for the contest.</param>
        /// <returns>
        /// An ActionResult containing the contest ID when the upsert succeeds and matches <paramref name="contestId"/> (201 Created),
        /// a 400 Bad Request with a message if the upsert succeeds but the returned ID does not match <paramref name="contestId"/>,
        /// or the handler's resulting action result on failure.
        /// </returns>
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

        /// <summary>
        /// Enqueues a background job to refresh AI existence and returns an accepted response containing a correlation identifier.
        /// </summary>
        /// <returns>An HTTP 202 Accepted response whose body is the correlation id for the enqueued refresh operation.</returns>
        [HttpPost]
        [Route("ai-refresh")]
        public IActionResult RefreshAiExistence()
        {
            var correlationId = Guid.NewGuid();
            var command = new RefreshAiExistenceCommand { CorrelationId = correlationId };
            _backgroundJobProvider.Enqueue<IRefreshAiExistenceCommandHandler>(p => p.ExecuteAsync(command, CancellationToken.None));
            return Accepted(correlationId);
        }

        /// <summary>
        /// Enqueues an asynchronous audit of AI-generated previews and returns a correlation identifier.
        /// </summary>
        /// <returns>Accepted (202) containing the correlation id for the enqueued audit.</returns>
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
        /// Retrieves the AI-generated matchup preview for the specified contest.
        /// </summary>
        /// <param name="contestId">The unique identifier of the contest to fetch the matchup preview for.</param>
        /// <returns>The matchup preview text when successful; otherwise an appropriate HTTP error result.</returns>
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

        /// <summary>
        /// Retrieves competitions that have no associated competitors.
        /// </summary>
        /// <returns>An ActionResult containing a list of CompetitionWithoutCompetitorsDto representing competitions without competitors.</returns>
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

        /// <summary>
        /// Retrieves competitions that have no recorded plays.
        /// </summary>
        /// <returns>A list of competitions that have no recorded play records.</returns>
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

        /// <summary>
        /// Retrieves competitions that have no drives.
        /// </summary>
        /// <returns>A 200 response containing a list of <see cref="CompetitionWithoutDrivesDto"/> when successful, or an error <see cref="ActionResult"/> otherwise.</returns>
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

        /// <summary>
        /// Retrieves competitions that have no associated metrics.
        /// </summary>
        /// <returns>An ActionResult containing a list of CompetitionWithoutMetricsDto for competitions that lack metrics.</returns>
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

        /// <summary>
        /// Execute an AI response query and return the handler's result.
        /// </summary>
        /// <param name="query">The query containing the prompt and options for generating the AI response.</param>
        /// <returns>An ActionResult containing the AI response string on success, or an appropriate error result otherwise.</returns>
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
        /// <summary>
        /// Initiates a backfill of league scores for the specified season and returns the operation result.
        /// </summary>
        /// <param name="seasonYear">The year of the season to backfill scores for.</param>
        /// <returns>A <see cref="BackfillLeagueScoresResult"/> describing the outcome of the backfill operation.</returns>
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
    }
}