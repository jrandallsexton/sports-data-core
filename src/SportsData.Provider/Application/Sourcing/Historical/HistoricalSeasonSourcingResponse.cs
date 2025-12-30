namespace SportsData.Provider.Application.Sourcing.Historical;

/// <summary>
/// Response from initiating historical season sourcing.
/// </summary>
public record HistoricalSeasonSourcingResponse
{
    /// <summary>
    /// Correlation ID for tracking the sourcing job in logs and monitoring.
    /// </summary>
    public required Guid CorrelationId { get; init; }

    /// <summary>
    /// Optional message with additional context (e.g., force reschedule status, warnings).
    /// </summary>
    public string? Message { get; init; }
}
