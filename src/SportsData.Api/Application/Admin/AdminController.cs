using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin;
using SportsData.Api.Application.Previews;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.AI;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Admin
{
    [ApiController]
    [Route("admin")]
    [AdminApiToken]
    public class AdminController : ApiControllerBase
    {
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
        private readonly IProvideAiCommunication _ai;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IAdminService _adminService;

        public AdminController(
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            IProvideAiCommunication ai,
            IProvideBackgroundJobs backgroundJobProvider,
            IAdminService adminService)
        {
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
            _ai = ai;
            _backgroundJobProvider = backgroundJobProvider;
            _adminService = adminService;
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

        [HttpPost]
        [Route("ai-test")]
        public async Task<IActionResult> TestAiCommunications([FromBody] AiChatCommand command)
        {
            return Ok(await _ai.GetResponseAsync(command.Text));
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
        public async Task<ActionResult<List<CompetitionWithoutCompetitorsDto>>> GetCompetitionsWithoutCompetitors()
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
    }

    public class GenerateUrlIdentityCommand
    {
        public string Url { get; set; } = string.Empty;
    }

    public class AiChatCommand
    {
        public required string Name { get; set; }
        public required string Text { get; set; }
    }
}
