using System;

namespace SportsData.Core.Eventing.Events.Contests;

/// <summary>
/// Published when a competitor's score is updated during a contest.
/// Allows consumers to update Contest.HomeScore and Contest.AwayScore in real-time.
/// </summary>
public record CompetitorScoreUpdated(
    Guid ContestId,
    Guid FranchiseSeasonId,
    int Score,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(CorrelationId, CausationId);
