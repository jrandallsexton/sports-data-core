using SportsData.Core.Common;

namespace SportsData.Provider.Application.Sourcing.Historical.Saga;

/// <summary>
/// Event published when historical season sourcing begins.
/// Initiates the saga state machine to orchestrate multi-tier processing.
/// </summary>
public record SeasonSourcingStarted(
    Guid CorrelationId,
    Sport Sport,
    int SeasonYear,
    SourceDataProvider Provider
);
