namespace SportsData.Provider.Application.Jobs;

/// <summary>
/// Hangfire job that simulates processing work for KEDA load testing.
/// Includes random delay to create backpressure in the job queue.
/// </summary>
public interface IProcessLoadTestJob
{
    /// <summary>
    /// Processes a load test job with simulated work and random delay.
    /// </summary>
    Task ProcessAsync(Guid testId, int batchNumber, int jobNumber, DateTime publishedUtc);
}

public class ProcessLoadTestJob : IProcessLoadTestJob
{
    private readonly ILogger<ProcessLoadTestJob> _logger;

    public ProcessLoadTestJob(ILogger<ProcessLoadTestJob> logger)
    {
        _logger = logger;
    }

    public async Task ProcessAsync(Guid testId, int batchNumber, int jobNumber, DateTime publishedUtc)
    {
        var startedAt = DateTime.UtcNow;
        var queueLatency = (startedAt - publishedUtc).TotalMilliseconds;

        _logger.LogInformation(
            "[KEDA-Test] Starting load test job. TestId={TestId}, Batch={BatchNumber}, Job={JobNumber}, QueueLatency={QueueLatencyMs}ms",
            testId, batchNumber, jobNumber, queueLatency);

        // Simulate varying workloads: 8000-15000ms processing time (8-15 seconds, longer than Producer to test different scaling behaviors)
        var processingTimeMs = Random.Shared.Next(8000, 15001);
        await Task.Delay(processingTimeMs);

        var completedAt = DateTime.UtcNow;
        var totalDuration = (completedAt - startedAt).TotalMilliseconds;

        _logger.LogInformation(
            "[KEDA-Test] Completed load test job. TestId={TestId}, Batch={BatchNumber}, Job={JobNumber}, ProcessingTime={ProcessingTimeMs}ms, TotalDuration={TotalDurationMs}ms",
            testId, batchNumber, jobNumber, processingTimeMs, totalDuration);
    }
}
