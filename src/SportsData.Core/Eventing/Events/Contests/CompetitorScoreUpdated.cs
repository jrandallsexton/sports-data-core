using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests;

/// <summary>
/// Published when a competitor's score is updated during a contest.
/// Allows consumers to update Contest.HomeScore and Contest.AwayScore in real-time.
/// </summary>
public record CompetitorScoreUpdated(
    Guid ContestId,
    Guid FranchiseSeasonId,
    int Score,
    Uri? Ref,
    Sport Sport,
    int? SeasonYear,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
