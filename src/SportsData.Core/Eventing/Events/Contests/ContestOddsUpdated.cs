using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests;

/// <summary>
/// A contest's odds changed (football: one provider's row; baseball: the
/// provider set from one wrapper document).
/// </summary>
/// <param name="OldSpread">
/// The changed provider's previous spread (football only; null on baseball).
/// Old*/New* describe THAT provider, whichever it is; they are what the
/// Notification line-move consumer reads, and are unchanged by
/// <paramref name="DisplayedOdds"/>.
/// </param>
/// <param name="DisplayedOdds">
/// The displayed-row snapshot (see <see cref="DisplayedContestOdds"/>) when
/// this event's odds include the row the matchup cards read; null otherwise
/// (another book changed), and null from pods publishing the prior shape
/// during a rolling deploy. Consumers must treat null as "nothing to apply".
/// </param>
public record ContestOddsUpdated(
    Guid ContestId,
    string Message,
    string? ProviderId,
    string? ProviderName,
    decimal? OldSpread,
    decimal? NewSpread,
    decimal? OldOverUnder,
    decimal? NewOverUnder,
    Uri? Ref,
    Sport Sport,
    int? SeasonYear,
    Guid CorrelationId,
    Guid CausationId,
    DisplayedContestOdds? DisplayedOdds = null
) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);