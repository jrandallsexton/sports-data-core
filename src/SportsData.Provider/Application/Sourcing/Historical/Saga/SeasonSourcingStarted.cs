using SportsData.Core.Common;

namespace SportsData.Provider.Application.Sourcing.Historical.Saga;

/// <summary>
/// Event published when historical season sourcing begins.
/// Initiates the saga state machine to orchestrate multi-tier processing.
/// </summary>
public record SeasonSourcingStarted
{
    public Guid CorrelationId { get; init; }
    public Sport Sport { get; init; }
    public int SeasonYear { get; init; }
    public SourceDataProvider Provider { get; init; }

    public SeasonSourcingStarted() { }

    public SeasonSourcingStarted(Guid correlationId, Sport sport, int seasonYear, SourceDataProvider provider)
    {
        CorrelationId = correlationId;
        Sport = sport;
        SeasonYear = seasonYear;
        Provider = provider;
    }
}
