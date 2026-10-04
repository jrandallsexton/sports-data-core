using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Contests.Baseball;
using SportsData.Core.Eventing.Events.Contests.Football;

namespace SportsData.Api.Application.SignalRDebug;

/// <summary>
/// SignalR debug broadcasts for the admin SignalR debug page. Each call
/// publishes a hand-built event that fans out to EVERY connected client, so
/// these are the most dangerous admin tools; [AdminApiToken] is on the class.
/// </summary>
[ApiController]
[Route("api/signalr-debug")]
[AdminApiToken]
public class SignalRDebugController : ApiControllerBase
{
    private readonly IEventBus _eventBus;
    private readonly IMessageDeliveryScope _deliveryScope;
    private readonly ILogger<SignalRDebugController> _logger;

    public SignalRDebugController(
        IEventBus eventBus,
        IMessageDeliveryScope deliveryScope,
        ILogger<SignalRDebugController> logger)
    {
        _eventBus = eventBus;
        _deliveryScope = deliveryScope;
        _logger = logger;
    }

    [HttpPost("contest-status")]
    public async Task<IActionResult> BroadcastDebugContestStatus(
        [FromBody] DebugContestStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Sport>(request.Sport, ignoreCase: true, out var sport))
            return BadRequest($"Unknown sport '{request.Sport}'.");

        // Explicit whitelist — Sport enum includes values (e.g.
        // BasketballNba) the debug harness has no sandbox ContestId
        // for. TryParse alone would accept them and silently fall
        // through to the Football branch.
        Guid contestId;
        switch (sport)
        {
            case Sport.BaseballMlb:
                contestId = SignalRDebugContestIds.Baseball;
                break;
            case Sport.FootballNcaa:
            case Sport.FootballNfl:
                contestId = SignalRDebugContestIds.Football;
                break;
            default:
                return BadRequest($"Unsupported sport '{request.Sport}' for SignalR debug harness.");
        }

        var correlationId = Guid.NewGuid();

        // No DbContext write here, so bypass the MassTransit outbox and
        // publish straight to the broker. UseBusOutbox would otherwise
        // require a SaveChangesAsync to flush, which we have nothing to save.
        using (_deliveryScope.Use(DeliveryMode.Direct))
        {
            await _eventBus.Publish(new ContestStatusChanged(
                ContestId: contestId,
                Status: request.Status,
                StatusDescription: request.StatusDescription,
                Ref: null,
                Sport: sport,
                SeasonYear: null,
                CorrelationId: correlationId,
                CausationId: CausationId.Api.SignalRDebugBroadcaster
            ), cancellationToken);
        }

        var safeStatus = request.Status?
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);

        _logger.LogInformation(
            "SignalRDebug: published ContestStatusChanged. ContestId={ContestId}, Sport={Sport}, Status={Status}, CorrelationId={CorrelationId}",
            contestId, sport, safeStatus, correlationId);

        return Accepted(new { contestId, correlationId });
    }

    [HttpPost("football-play")]
    public async Task<IActionResult> BroadcastDebugFootballPlay(
        [FromBody] DebugFootballPlayRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Sport>(request.Sport, ignoreCase: true, out var sport))
            return BadRequest($"Unknown sport '{request.Sport}'.");

        // football-play is football-only by definition — reject
        // any other sport rather than publishing a FootballPlayCompleted
        // for them.
        if (sport is not (Sport.FootballNcaa or Sport.FootballNfl))
            return BadRequest($"Unsupported sport '{request.Sport}' for football-play debug endpoint.");

        var contestId = SignalRDebugContestIds.Football;
        var correlationId = Guid.NewGuid();

        using (_deliveryScope.Use(DeliveryMode.Direct))
        {
            await _eventBus.Publish(new FootballPlayCompleted(
                ContestId: contestId,
                CompetitionId: contestId, // sandbox: reuse contestId so consumers don't need a real competition row
                PlayId: Guid.NewGuid(),
                PlayDescription: request.PlayDescription,
                Period: request.Period,
                Clock: request.Clock,
                AwayScore: request.AwayScore,
                HomeScore: request.HomeScore,
                PossessionFranchiseSeasonId: request.PossessionFranchiseSeasonId,
                IsScoringPlay: request.IsScoringPlay,
                ScoringPlayType: request.ScoringPlayType,
                BallOnYardLine: request.BallOnYardLine,
                Down: request.Down,
                Distance: request.Distance,
                Ref: null,
                Sport: sport,
                SeasonYear: null,
                CorrelationId: correlationId,
                CausationId: CausationId.Api.SignalRDebugBroadcaster
            ), cancellationToken);
        }

        var sanitizedPeriodForLog = request.Period?.ToString()?.Replace("\r", "").Replace("\n", "");
        var sanitizedClockForLog = request.Clock?.Replace("\r", "").Replace("\n", "");
        _logger.LogInformation(
            "SignalRDebug: published FootballPlayCompleted. ContestId={ContestId}, Period={Period}, Clock={Clock}, Score={Away}-{Home}, Yard={Yard}, Scoring={Scoring}, CorrelationId={CorrelationId}",
            contestId, sanitizedPeriodForLog, sanitizedClockForLog, request.AwayScore, request.HomeScore, request.BallOnYardLine, request.IsScoringPlay, correlationId);

        return Accepted(new { contestId, correlationId });
    }

    [HttpPost("baseball-play")]
    public async Task<IActionResult> BroadcastDebugBaseballPlay(
        [FromBody] DebugBaseballPlayRequest request,
        CancellationToken cancellationToken)
    {
        var contestId = SignalRDebugContestIds.Baseball;
        var correlationId = Guid.NewGuid();

        using (_deliveryScope.Use(DeliveryMode.Direct))
        {
            await _eventBus.Publish(new BaseballPlayCompleted(
                ContestId: contestId,
                CompetitionId: contestId, // sandbox: reuse contestId so consumers don't need a real competition row
                PlayId: Guid.NewGuid(),
                PlayDescription: request.PlayDescription,
                Inning: request.Inning,
                HalfInning: request.HalfInning,
                AwayScore: request.AwayScore,
                HomeScore: request.HomeScore,
                Balls: request.Balls,
                Strikes: request.Strikes,
                Outs: request.Outs,
                RunnerOnFirst: request.RunnerOnFirst,
                RunnerOnSecond: request.RunnerOnSecond,
                RunnerOnThird: request.RunnerOnThird,
                AtBatAthleteSeasonId: request.AtBatAthleteSeasonId,
                AtBatShortName: request.AtBatShortName,
                AtBatPositionAbbreviation: request.AtBatPositionAbbreviation,
                AtBatHeadshotUrl: request.AtBatHeadshotUrl,
                PitchingAthleteSeasonId: request.PitchingAthleteSeasonId,
                PitchingShortName: request.PitchingShortName,
                PitchingPositionAbbreviation: request.PitchingPositionAbbreviation,
                PitchingHeadshotUrl: request.PitchingHeadshotUrl,
                Ref: null,
                Sport: Sport.BaseballMlb,
                SeasonYear: null,
                CorrelationId: correlationId,
                CausationId: CausationId.Api.SignalRDebugBroadcaster
            ), cancellationToken);
        }

        var halfInningForLog = (request.HalfInning ?? string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);

        _logger.LogInformation(
            "SignalRDebug: published BaseballPlayCompleted. ContestId={ContestId}, Inning={Half} {Inning}, Score={Away}-{Home}, Count={Balls}-{Strikes}, Outs={Outs}, CorrelationId={CorrelationId}",
            contestId, halfInningForLog, request.Inning, request.AwayScore, request.HomeScore, request.Balls, request.Strikes, request.Outs, correlationId);

        return Accepted(new { contestId, correlationId });
    }
}
