using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests;

/// <summary>
/// Published when a contest's score changes during a live game.
/// This is an integration event sent to the API project for SignalR broadcasting.
/// </summary>
public record ContestScoreChanged(
    Guid ContestId,
    Guid FranchiseSeasonId,
    int Score,
    Uri? Ref,
    Sport Sport,
    int? SeasonYear,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
