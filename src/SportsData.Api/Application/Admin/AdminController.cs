using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Previews;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Extensions;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.Clients.AI;
using SportsData.Core.Processing;

using static SportsData.Api.Application.Admin.AdminService;

namespace SportsData.Api.Application.Admin
{
    [ApiController]
    [Route("[controller]")]
    [AdminApiToken]
    public class AdminController : ControllerBase
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
        public IActionResult GenerateUrlIdentity([FromBody] GenerateUrlIdentityCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.Url))
            {
                return BadRequest("URL cannot be empty.");
            }
            // Here you would typically generate a unique identity based on the URL.
            // For simplicity, we will just return the URL as is.
            var identity = _externalRefIdentityGenerator.Generate(command.Url);

            return Ok(identity);
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
        [Route("matchup/{contestId}/preview/{previewId}/approve")]
        public async Task<IActionResult> ApproveContestPreview([FromRoute] Guid contestId, [FromRoute] Guid previewId)
        {
            var userId = HttpContext.GetCurrentUserId();

            var cmd = new ApproveMatchupPreviewCommand
            {
                PreviewId = previewId,
                ContestId = contestId,
                ApprovedByUserId = userId
            };

            var approvalResult = await _adminService.ApproveMatchupPreview(cmd);

            if (approvalResult != previewId)
            {
                return BadRequest();
            }

            return Ok(approvalResult);
        }

        [HttpPost]
        [Authorize]
        [Route("matchup/{contestId}/preview/reject")]
        public async Task<IActionResult> RejectContestPreview([FromBody] RejectMatchupPreviewCommand command)
        {
            var userId = HttpContext.GetCurrentUserId();

            command.RejectedByUserId = userId;

            var rejectionResult = await _adminService.RejectMatchupPreview(command);

            if (rejectionResult != command.PreviewId)
            {
                return BadRequest();
            }

            var cmd = new GenerateMatchupPreviewsCommand
            {
                ContestId = command.ContestId
            };

            _backgroundJobProvider.Enqueue<IGenerateMatchupPreviews>(p => p.Process(cmd));

            return Accepted(new { cmd.CorrelationId });
        }

        [HttpPost]
        [Route("matchup/preview/{contestId}")]
        public async Task<IActionResult> UpsertContestPreview(
            [FromRoute] Guid contestId,
            [FromBody] string matchupPreview)
        {
            var result = await _adminService.UpsertMatchupPreview(matchupPreview);

            if (result == contestId)
                return Created();
            else
                return BadRequest("The provided preview does not match the specified contest ID.");
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
        public async Task<IActionResult> GetAiPreview([FromRoute] Guid contestId)
        {
            return Ok(await _adminService.GetMatchupPreview(contestId));
        }

        [HttpGet]
        [Route("errors/competitions-without-competitors")]
        public async Task<IActionResult> GetCompetitionsWithoutCompetitors()
        {
            try 
            {
                var result = await _adminService.GetCompetitionsWithoutCompetitors();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
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
