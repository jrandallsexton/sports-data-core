using SportsData.Core.Common;

namespace SportsData.Api.Application.Matchups.Commands.BackfillMatchupOddsPricing;

/// <summary>
/// Populate odds pricing (per-team moneyline and spread price, over/under
/// prices) on every PickemGroupMatchup: one background job per distinct
/// contest. Stopgap until the matchup data flows carry these fields.
/// </summary>
public record BackfillMatchupOddsPricingCommand;

/// <param name="CorrelationId">Shared by every enqueued job; the Seq handle for the run.</param>
public record BackfillMatchupOddsPricingResult(
    Guid CorrelationId,
    int ContestsEnqueued,
    List<BackfillMatchupOddsPricingSportResult> Sports);

public record BackfillMatchupOddsPricingSportResult(Sport Sport, int ContestsEnqueued);

/// <summary>One contest's pricing job: fetch from that sport's Producer, apply to every matchup carrying it.</summary>
public record ApplyMatchupOddsPricingCommand(Sport Sport, Guid ContestId, Guid CorrelationId);
