using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;

namespace SportsData.Producer.Application.Competitions.InstantAnalysis.Lab;

/// <summary>
/// Admin replay lab for instant analysis (task 09). Producer is internal only;
/// the API exposes these behind its admin gate. Football Producers only: the
/// handlers aren't registered on other sports.
/// </summary>
[Route("api/instant-analysis/lab")]
[ApiController]
public class InstantAnalysisLabController : ControllerBase
{
    [HttpPost("contests/{contestId:guid}/runs")]
    public async Task<ActionResult<PlayAnalysisRunSummaryDto>> RunLab(
        [FromRoute] Guid contestId,
        [FromServices] IRunPlayAnalysisLabCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(contestId, GetCorrelationIdFromRequest(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("contests/{contestId:guid}/runs")]
    public async Task<ActionResult<List<PlayAnalysisRunSummaryDto>>> GetRuns(
        [FromRoute] Guid contestId,
        [FromServices] IGetPlayAnalysisRunsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(contestId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("runs/{runId:guid}")]
    public async Task<ActionResult<PlayAnalysisRunDetailDto>> GetRun(
        [FromRoute] Guid runId,
        [FromServices] IGetPlayAnalysisRunQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(runId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("facts/{playAnalysisId:guid}/rating")]
    public async Task<ActionResult<PlayAnalysisRatingDto>> Rate(
        [FromRoute] Guid playAnalysisId,
        [FromBody] RatePlayAnalysisRequest request,
        [FromServices] IRatePlayAnalysisCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(playAnalysisId, request.Stars, GetCorrelationIdFromRequest(), cancellationToken);
        return result.ToActionResult();
    }

    private Guid GetCorrelationIdFromRequest() =>
        Request.Headers.TryGetValue("X-Correlation-Id", out var headerValue) && Guid.TryParse(headerValue, out var inbound)
            ? inbound
            : ActivityExtensions.GetCorrelationId();
}
