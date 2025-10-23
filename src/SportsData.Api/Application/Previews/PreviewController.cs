using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Extensions;
using SportsData.Core.Processing;

using static SportsData.Api.Application.Admin.AdminService;

namespace SportsData.Api.Application.Previews
{
    [ApiController]
    [Route("preview")]
    public class PreviewController : ControllerBase
    {
        private readonly IPreviewService _previewService;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public PreviewController(
            IPreviewService previewService,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _previewService = previewService;
            _backgroundJobProvider = backgroundJobProvider;
        }

        [HttpPost]
        [Authorize]
        [Route("{previewId}/approve")]
        public async Task<IActionResult> ApproveContestPreview([FromRoute] Guid previewId)
        {
            var userId = HttpContext.GetCurrentUserId();

            var cmd = new ApproveMatchupPreviewCommand
            {
                PreviewId = previewId,
                ApprovedByUserId = userId
            };

            var approvalResult = await _previewService.ApproveMatchupPreview(cmd);

            if (approvalResult != previewId)
            {
                return BadRequest();
            }

            return Ok(approvalResult);
        }

        [HttpPost]
        [Authorize]
        [Route("{previewId}/reject")]
        public async Task<IActionResult> RejectContestPreview([FromBody] RejectMatchupPreviewCommand command)
        {
            var userId = HttpContext.GetCurrentUserId();

            command.RejectedByUserId = userId;

            var rejectionResult = await _previewService.RejectMatchupPreview(command);

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
    }
}
