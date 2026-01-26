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
    /// Target service: 'producer', 'provider', or 'both'.
    /// </summary>
    public string Target { get; set; } = "both";

    /// <summary>
    /// Number of events to publish per batch (1-100).
    /// </summary>
    public int BatchSize { get; set; } = 10;
}
