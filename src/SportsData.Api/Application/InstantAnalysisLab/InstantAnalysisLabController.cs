using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Infrastructure.Auth;
using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;

namespace SportsData.Api.Application.InstantAnalysisLab;

/// <summary>
/// Instant-analysis replay lab's API surface (web admin → here → Producer).
/// Thin relays through the sport's Producer contest client, like the contest
/// replay endpoint; the route's sport and league pick the Producer pod.
/// Football only (Producer registers the lab on football pods).
/// See docs/features/instant-analysis/09-replay-lab.md.
/// </summary>
[ApiController]
[Route("api/{sport}/{league}/instant-analysis-lab")]
[AdminApiToken]
public class InstantAnalysisLabController : ControllerBase
{
    /// <summary>
    /// The lab exists only on football Producer pods; any other sport would
    /// reach a Producer with no lab handlers and fail as a 500. Null when the
    /// sport is football.
    /// </summary>
    private ActionResult? RejectNonFootball(Sport mode) =>
        mode is Sport.FootballNcaa or Sport.FootballNfl
            ? null
            : BadRequest($"The instant-analysis lab supports football only, not '{mode}'.");

    /// <summary>Replays a completed game's plays as a new lab run; returns when the run is complete.</summary>
    [HttpPost("contests/{contestId:guid}/runs")]
    public async Task<ActionResult<PlayAnalysisRunSummaryDto>> RunLab(
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] Guid contestId,
        [FromServices] IContestClientFactory contestClientFactory,
        CancellationToken cancellationToken)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        if (RejectNonFootball(mode) is { } rejected)
            return rejected;

        var result = await contestClientFactory
            .Resolve(mode)
            .RunInstantAnalysisLab(contestId, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("contests/{contestId:guid}/runs")]
    public async Task<ActionResult<List<PlayAnalysisRunSummaryDto>>> GetRuns(
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] Guid contestId,
        [FromServices] IContestClientFactory contestClientFactory,
        CancellationToken cancellationToken)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        if (RejectNonFootball(mode) is { } rejected)
            return rejected;

        var result = await contestClientFactory
            .Resolve(mode)
            .GetInstantAnalysisLabRuns(contestId, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("runs/{runId:guid}")]
    public async Task<ActionResult<PlayAnalysisRunDetailDto>> GetRun(
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] Guid runId,
        [FromServices] IContestClientFactory contestClientFactory,
        CancellationToken cancellationToken)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        if (RejectNonFootball(mode) is { } rejected)
            return rejected;

        var result = await contestClientFactory
            .Resolve(mode)
            .GetInstantAnalysisLabRun(runId, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPut("facts/{playAnalysisId:guid}/rating")]
    public async Task<ActionResult<PlayAnalysisRatingDto>> Rate(
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] Guid playAnalysisId,
        [FromBody] RatePlayAnalysisRequest request,
        [FromServices] IContestClientFactory contestClientFactory,
        CancellationToken cancellationToken)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        if (RejectNonFootball(mode) is { } rejected)
            return rejected;

        var result = await contestClientFactory
            .Resolve(mode)
            .RateInstantAnalysisFact(playAnalysisId, request.Stars, cancellationToken);

        return result.ToActionResult();
    }
}
