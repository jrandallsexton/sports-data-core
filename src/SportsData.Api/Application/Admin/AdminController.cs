using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin;
using SportsData.Api.Application.AI;
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
        private readonly IAdminService _adminService;
        private readonly IAiService _aiService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            IProvideBackgroundJobs backgroundJobProvider,
            IAdminService adminService,
            IAiService aiService,
            ILogger<AdminController> logger)
        {
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
            _backgroundJobProvider = backgroundJobProvider;
            _adminService = adminService;
            _aiService = aiService;
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
            [FromBody] GenerateGameRecapCommand command)
        {
            try
            {
                var response = await _aiService.GenerateGameRecapAsync(command, CancellationToken.None);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Game recap generation failed with invalid operation");
                return BadRequest(new { error = "Failed to generate game recap", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error generating game recap");
                return StatusCode(500, new
                {
                    error = "Failed to generate game recap",
                    message = ex.Message
                });
            }
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
            [FromBody] string matchupPreview)
        {
            var result = await _adminService.UpsertMatchupPreview(matchupPreview);

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
            _backgroundJobProvider.Enqueue<IAdminService>(p => p.RefreshAiExistence(correlationId));
            return Accepted(correlationId);
        }

        [HttpPost]
        [Route("ai-audit")]
        public IActionResult AiPreviewsAudit()
        {
            var correlationId = Guid.NewGuid();
            _backgroundJobProvider.Enqueue<IAdminService>(p => p.AuditAi(correlationId));
            return Accepted(correlationId);
        }

        [HttpGet]
        [Route("matchup/preview/{contestId}")]
        public async Task<ActionResult<string>> GetAiPreview([FromRoute] Guid contestId)
        {
            var result = await _adminService.GetMatchupPreview(contestId);
            return result.ToActionResult();
        }

        [HttpGet]
        [Route("errors/competitions-without-competitors")]
        public async Task<ActionResult<GetCompetitionsWithoutCompetitorsResponse>> GetCompetitionsWithoutCompetitors()
        {
            var result = await _adminService.GetCompetitionsWithoutCompetitors();
            return result.ToActionResult();
        }

        [HttpGet]
        [Route("errors/competitions-without-plays")]
        public async Task<ActionResult<List<CompetitionWithoutPlaysDto>>> GetCompetitionsWithoutPlays()
        {
            var result = await _adminService.GetCompetitionsWithoutPlays();
            return result.ToActionResult();
        }

        [HttpGet]
        [Route("errors/competitions-without-drives")]
        public async Task<ActionResult<List<CompetitionWithoutDrivesDto>>> GetCompetitionsWithoutDrives()
        {
            var result = await _adminService.GetCompetitionsWithoutDrives();
            return result.ToActionResult();
        }

        [HttpGet]
        [Route("errors/competitions-without-metrics")]
        public async Task<ActionResult<List<CompetitionWithoutMetricsDto>>> GetCompetitionsWithoutMetrics()
        {
            var result = await _adminService.GetCompetitionsWithoutMetrics();
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
        public async Task<IActionResult> TestAiCommunications([FromBody] AiChatCommand command)
        {
            var response = await _aiService.GetAiResponseAsync(command.Text);
            return Ok(response);
        }

        /// <summary>
        /// Backfills league week scores for an entire season.
        /// Processes all completed weeks for the specified season year.
        /// </summary>
        /// <param name="seasonYear">The season year to backfill (e.g., 2024, 2025)</param>
        /// <returns>Summary of backfill operation</returns>
        [HttpPost]
        [Route("backfill-league-scores/{seasonYear}")]
        public async Task<IActionResult> BackfillLeagueScores(int seasonYear)
        {
            try
            {
                var result = await _adminService.BackfillLeagueScoresAsync(seasonYear);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error during backfill for season {SeasonYear}",
                    seasonYear);

                return StatusCode(500, new
                {
                    seasonYear,
                    error = "Backfill failed",
                    message = ex.Message
                });
            }
        }
    }
}
