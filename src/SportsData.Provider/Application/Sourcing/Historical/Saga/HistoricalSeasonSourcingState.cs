using MassTransit;
using SportsData.Core.Common;

namespace SportsData.Provider.Application.Sourcing.Historical.Saga;

/// <summary>
/// Saga state for orchestrating historical season data sourcing across multiple tiers.
/// Tracks completion events from Producer to determine when to trigger the next tier.
/// </summary>
public class HistoricalSeasonSourcingState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = null!;
    
    // Sourcing context
    public Sport Sport { get; set; }
    public int SeasonYear { get; set; }
    public SourceDataProvider Provider { get; set; }
    
    // Optimistic concurrency token (PostgreSQL xmin system column)
    public uint RowVersion { get; set; }
    
    // Tier completion tracking - counts completion events received from Producer
    public int SeasonCompletionEventsReceived { get; set; }
    public int VenueCompletionEventsReceived { get; set; }
    public int TeamSeasonCompletionEventsReceived { get; set; }
    public int AthleteSeasonCompletionEventsReceived { get; set; }
    
    // Timestamps for observability
    public DateTime StartedUtc { get; set; }
    public DateTime? SeasonCompletedUtc { get; set; }
    public DateTime? VenueCompletedUtc { get; set; }
    public DateTime? TeamSeasonCompletedUtc { get; set; }
    public DateTime? AthleteSeasonCompletedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}
