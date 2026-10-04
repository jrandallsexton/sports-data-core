using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Admin;
using SportsData.Api.Application.Previews.Commands.ApproveMatchupPreview;
using SportsData.Api.Application.Previews.Commands.GenerateMatchupPreviews;
using SportsData.Api.Application.Previews.Commands.RejectMatchupPreview;
using SportsData.Api.Application.Previews.Commands.UpsertMatchupPreview;
using SportsData.Api.Application.Previews.Jobs.Generation;
using SportsData.Api.Application.Previews.Queries.GetMatchupPreview;
using SportsData.Api.Application.Previews.Queries.GetMatchupPreviewCaptures;
using SportsData.Api.Extensions;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Previews
{
    /// <summary>
    /// Matchup previews: approve/reject by preview id, and the contest-keyed
    /// operator actions (generate/reset, capture, experiment, panel, captures).
    /// All are admin actions (the web app shows them only to admins);
    /// [AdminApiToken] enforces that at the API, accepting the Admin-role JWT
    /// the web app sends. Previously plain [Authorize]: any signed-in user.
    /// </summary>
    [ApiController]
    [Route("api/previews")]
    [AdminApiToken]
    public class PreviewsController : ControllerBase
    {
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public PreviewsController(IProvideBackgroundJobs backgroundJobProvider)
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
    

        [HttpGet("contests/{contestId}")]
        public async Task<ActionResult<string>> GetAiPreview(
            [FromRoute] Guid contestId,
            [FromServices] IGetMatchupPreviewQueryHandler handler,
            CancellationToken cancellationToken)
        {
            var query = new GetMatchupPreviewQuery(contestId);
            var result = await handler.ExecuteAsync(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("contests/{contestId}")]
        public async Task<ActionResult<Guid>> UpsertContestPreview(
            [FromRoute] Guid contestId,
            [FromBody] string matchupPreview,
            [FromServices] IUpsertMatchupPreviewCommandHandler handler,
            CancellationToken cancellationToken)
        {
            var command = new UpsertMatchupPreviewCommand(matchupPreview);
            var result = await handler.ExecuteAsync(command, cancellationToken);

            if (result.IsSuccess && result.Value == contestId)
                return Created($"/api/previews/contests/{contestId}", new { contestId });

            if (result.IsSuccess)
                return BadRequest("The provided preview does not match the specified contest ID.");

            return result.ToActionResult();
        }

        [HttpPost("contests/{contestId}/reset")]
        public IActionResult ResetContestPreview(
            [FromRoute] Guid contestId,
            // Sport enum name (e.g. "FootballNfl"); omitted = NCAA for
            // backward compatibility. The processor validates by resolving
            // the contest against this sport's canonical client — a wrong
            // sport 404s there and is logged as a skip.
            [FromQuery] Sport sport = Sport.FootballNcaa)
        {
            var cmd = new GenerateMatchupPreviewsCommand
            {
                ContestId = contestId,
                Sport = sport
            };
            _backgroundJobProvider.Enqueue<IGenerateMatchupPreviews>(p => p.Process(cmd));
            return Accepted(new { cmd.CorrelationId });
        }

        /// <summary>
        /// Dry run: assemble and persist the exact prompt payload for a
        /// contest WITHOUT calling the model or writing a preview. Completed
        /// contests are allowed (backtest capture). Completion is announced
        /// via SignalR (PreviewPromptCaptured); retrieve results from the
        /// captures endpoint below.
        /// </summary>
        [HttpPost("contests/{contestId}/capture")]
        public IActionResult CaptureContestPreviewPrompt(
            [FromRoute] Guid contestId,
            [FromQuery] Sport sport = Sport.FootballNcaa,
            // Prompt entity Guid — model binding rejects malformed values
            // with a 400 before anything reaches Hangfire.
            [FromQuery] Guid? promptId = null)
        {
            var cmd = new GenerateMatchupPreviewsCommand
            {
                ContestId = contestId,
                Sport = sport,
                Mode = PreviewGenerationMode.Capture,
                PromptId = promptId
            };
            _backgroundJobProvider.Enqueue<IGenerateMatchupPreviews>(p => p.Process(cmd));
            return Accepted(new { cmd.CorrelationId });
        }

        /// <summary>
        /// Persisted prompt captures for a contest, newest first — payload,
        /// metadata, and the full rendered prompt exactly as the model would
        /// receive it (instruction blob + payload + editor note).
        /// </summary>
        [HttpGet("contests/{contestId}/captures")]
        public async Task<ActionResult<List<MatchupPreviewCaptureDto>>> GetContestPreviewPromptCaptures(
            [FromRoute] Guid contestId,
            [FromServices] IGetMatchupPreviewCapturesQueryHandler handler,
            CancellationToken cancellationToken)
        {
            var result = await handler.ExecuteAsync(new GetMatchupPreviewCapturesQuery(contestId), cancellationToken);
            return result.ToActionResult();
        }

        /// <summary>
        /// Eval run: assemble the prompt, call the model, and store the raw
        /// response on the capture row. NEVER writes a MatchupPreview — safe
        /// to run against contests that already have a real preview from a
        /// prior season (the picks page reads newest-non-rejected, so an
        /// experimental preview row would shadow the real one). Completed
        /// contests allowed. Completion announced via SignalR
        /// (PreviewPromptCaptured).
        /// </summary>
        [HttpPost("contests/{contestId}/experiment")]
        public IActionResult RunContestPreviewExperiment(
            [FromRoute] Guid contestId,
            [FromQuery] Sport sport = Sport.FootballNcaa,
            [FromQuery] Guid? promptId = null,
            [FromQuery] Guid? modelId = null)
        {
            // modelId (optional): run against that Model row instead of the
            // production client — the Model Lab's single-cell fill-in.
            var cmd = new GenerateMatchupPreviewsCommand
            {
                ContestId = contestId,
                Sport = sport,
                Mode = PreviewGenerationMode.Experiment,
                PromptId = promptId,
                ModelId = modelId
            };
            _backgroundJobProvider.Enqueue<IGenerateMatchupPreviews>(p => p.Process(cmd));
            return Accepted(new { cmd.CorrelationId });
        }

        /// <summary>
        /// Model Consensus Lab fan-out: run the SAME experiment (same prompt
        /// assembly, same contest) against every active Model whose provider
        /// the lab can reach — one capture row per model, never a
        /// MatchupPreview. The audition in one call. Model/ModelProvider
        /// rows are managed by the existing admin CRUD (models /
        /// model-providers routes). See docs/features/model-consensus-lab.md.
        /// </summary>
        [HttpPost("contests/{contestId}/experiment/panel")]
        public async Task<IActionResult> RunContestPreviewPanel(
            [FromRoute] Guid contestId,
            [FromServices] AppDataContext dataContext,
            [FromServices] IAiModelClientResolver modelClientResolver,
            [FromQuery] Sport sport = Sport.FootballNcaa,
            [FromQuery] Guid? promptId = null,
            CancellationToken cancellationToken = default)
        {
            // Budget guard: experiment spend is approved, runaway loops are
            // not. 25 models per fan-out is far above any realistic audition.
            const int maxPanelSize = 25;

            var candidates = await dataContext.Models
                .AsNoTracking()
                .Where(x => x.IsActive && x.ModelProvider!.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Name, x.Gateway, x.ModelProvider!.Kind })
                .ToListAsync(cancellationToken);

            // The resolver is the single source of truth for which routes
            // have a lab client (today: the OpenRouter gateway; direct
            // first-party clients arrive with panel promotion).
            var models = candidates
                .Where(x => modelClientResolver.CanResolve(x.Gateway, x.Kind))
                .Take(maxPanelSize)
                .ToList();

            if (models.Count == 0)
                return UnprocessableEntity(new { error = "No active models under a lab-reachable provider." });

            var correlationId = Guid.NewGuid();
            foreach (var model in models)
            {
                var cmd = new GenerateMatchupPreviewsCommand
                {
                    ContestId = contestId,
                    Sport = sport,
                    Mode = PreviewGenerationMode.Experiment,
                    PromptId = promptId,
                    ModelId = model.Id,
                    CorrelationId = correlationId
                };
                _backgroundJobProvider.Enqueue<IGenerateMatchupPreviews>(p => p.Process(cmd));
            }

            return Accepted(new { correlationId, modelCount = models.Count });
        }
}
}
