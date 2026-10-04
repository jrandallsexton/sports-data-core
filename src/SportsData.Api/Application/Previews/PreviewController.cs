using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin;
using SportsData.Api.Application.Previews.Commands.ApproveMatchupPreview;
using SportsData.Api.Application.Previews.Commands.GenerateMatchupPreviews;
using SportsData.Api.Application.Previews.Commands.RejectMatchupPreview;
using SportsData.Api.Application.Previews.Jobs.Generation;
using SportsData.Api.Extensions;
using SportsData.Core.Extensions;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Previews
{
    /// <summary>
    /// Approve/reject are admin actions (the web app shows them only to admins);
    /// [AdminApiToken] enforces that at the API, accepting the Admin-role JWT
    /// the web app sends. Previously plain [Authorize]: any signed-in user.
    /// </summary>
    [ApiController]
    [Route("preview")]
    [AdminApiToken]
    public class PreviewController : ControllerBase
    {
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public PreviewController(IProvideBackgroundJobs backgroundJobProvider)
        {
            _backgroundJobProvider = backgroundJobProvider;
        }

        [HttpPost]
        [Route("{previewId}/approve")]
        public async Task<ActionResult<Guid>> ApproveContestPreview(
            [FromRoute] Guid previewId,
            [FromServices] IApproveMatchupPreviewCommandHandler handler,
            CancellationToken cancellationToken)
        {
            var userId = HttpContext.GetCurrentUserId();

            var cmd = new ApproveMatchupPreviewCommand
            {
                PreviewId = previewId,
                ApprovedByUserId = userId
            };

            var result = await handler.ExecuteAsync(cmd, cancellationToken);

            return result.ToActionResult();
        }

        [HttpPost]
        [Route("{previewId}/reject")]
        public async Task<IActionResult> RejectContestPreview(
            [FromBody] RejectMatchupPreviewCommand command,
            [FromServices] IRejectMatchupPreviewCommandHandler handler,
            CancellationToken cancellationToken)
        {
            var userId = HttpContext.GetCurrentUserId();

            command.RejectedByUserId = userId;

            var result = await handler.ExecuteAsync(command, cancellationToken);

            if (!result.IsSuccess)
            {
                return result.ToActionResult().Result!;
            }

            var cmd = new GenerateMatchupPreviewsCommand
            {
                ContestId = command.ContestId,
                Sport = command.Sport
            };

            _backgroundJobProvider.Enqueue<IGenerateMatchupPreviews>(p => p.Process(cmd));

            return Accepted(new { cmd.CorrelationId });
        }
    }
}
