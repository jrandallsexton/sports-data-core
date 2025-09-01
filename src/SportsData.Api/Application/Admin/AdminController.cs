using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Processors;
using SportsData.Api.Application.Scoring;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.Clients.AI;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Admin
{
    [ApiController]
    [Route("[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
        private readonly IProvideAiCommunication _ai;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public AdminController(
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            IProvideAiCommunication ai,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
            _ai = ai;
            _backgroundJobProvider = backgroundJobProvider;
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
        [Route("contest/{contestId}/score")]
        public IActionResult ScoreContest([FromRoute] Guid contestId)
        {
            var cmd = new ScoreContestCommand
            {
                ContestId = contestId
            };
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
