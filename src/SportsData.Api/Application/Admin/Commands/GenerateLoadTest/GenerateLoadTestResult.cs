
namespace SportsData.Api.Application.Admin.Commands.GenerateLoadTest;

/// <summary>
/// Result of a KEDA load test generation.
/// </summary>
public class GenerateLoadTestResult
{
    public required Guid TestId { get; init; }
    public required int EventsPublished { get; init; }
    public required LoadTestTarget Target { get; init; }
    public required int Batches { get; init; }
    public required int BatchSize { get; init; }
    public required DateTime PublishedUtc { get; init; }
    public required string Message { get; init; }
}
