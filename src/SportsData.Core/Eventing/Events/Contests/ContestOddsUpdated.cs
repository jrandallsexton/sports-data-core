using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests;

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
    Guid CausationId
) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);