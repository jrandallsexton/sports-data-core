using System;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.RequestSingleFranchiseSeasonSourcing;

/// <summary>
/// Re-source ONE franchise season from ESPN — the single-team twin of
/// <see cref="RequestFranchiseSeasonSourcing.RequestFranchiseSeasonSourcingCommand"/>.
/// Operators use it from the team page when a team's season is incompletely
/// sourced (e.g. a schedule missing games).
/// </summary>
public record RequestSingleFranchiseSeasonSourcingCommand(
    Guid FranchiseSeasonId,
    // Resolved by the controller from X-Correlation-Id (or the current activity).
    Guid CorrelationId);
