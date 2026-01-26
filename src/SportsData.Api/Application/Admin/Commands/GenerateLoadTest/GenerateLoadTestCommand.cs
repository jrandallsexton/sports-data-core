namespace SportsData.Api.Application.Admin.Commands.GenerateLoadTest;

/// <summary>
/// Command to generate synthetic load for KEDA autoscaling validation.
/// </summary>
public class GenerateLoadTestCommand
{
    /// <summary>
    /// Number of test jobs to create per target (1-1000).
    /// </summary>
    public int Count { get; set; } = 50;

    /// <summary>
    /// Target service: Producer, Provider, or Both.
    /// </summary>
    public LoadTestTarget Target { get; set; } = LoadTestTarget.Both;

    /// <summary>
    /// Number of events to publish per batch (1-100).
    /// </summary>
    public int BatchSize { get; set; } = 10;
}
