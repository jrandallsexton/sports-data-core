using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Matchups.Jobs.ApplyMatchupOdds;

/// <summary>Apply one contest's displayed odds to every PickemGroupMatchup carrying it.</summary>
/// <param name="AsOfUtc">
/// The odds version: the source event's CreatedUtc, stamped by the Producer
/// when it built the event. Compared against PickemGroupMatchup.OddsAsOfUtc.
/// </param>
public record ApplyMatchupOddsCommand(
    Guid ContestId,
    Sport Sport,
    DisplayedContestOdds Odds,
    Guid CorrelationId,
    DateTime AsOfUtc);