using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Processing;
using SportsData.Producer.Application.Documents.Commands.ReprocessDeadLetterQueue;

namespace SportsData.Producer.Controllers;

/// <summary>
/// Endpoints for managing the dead-letter queue (DLQ).
/// </summary>
[ApiController]
[Route("api/dead-letter")]
public class DeadLetterController : ControllerBase
{
    private readonly ILogger<DeadLetterController> _logger;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public DeadLetterController(
        ILogger<DeadLetterController> logger,
        IProvideBackgroundJobs backgroundJobProvider)
    {
        _logger = logger;
        _backgroundJobProvider = backgroundJobProvider;
    }

    /// <summary>
    /// Enqueues a background job that pulls messages from the RabbitMQ dead-letter queue
    /// and re-publishes them for normal processing.
    /// </summary>
    /// <param name="count">
    /// Maximum number of messages to reprocess. Defaults to 10.
    /// </param>
    /// <param name="queueName">
    /// Override the target DLQ name. Defaults to <c>document-dead-letter</c>
    /// (or the value of <c>SportsData.Producer:DeadLetterQueue:QueueName</c>
    /// in Azure AppConfig).
    /// </param>
    /// <param name="resetAttemptCount">
    /// When <c>true</c> (default), resets <c>AttemptCount</c> to 0 on each
    /// re-published message so the retry ladder starts fresh.
    /// </param>
    [HttpPost("reprocess")]
    public IActionResult Reprocess(
        [FromQuery] int count = 10,
        [FromQuery] string? queueName = null,
        [FromQuery] bool resetAttemptCount = true)
    {
        var command = new ReprocessDeadLetterQueueCommand(count, queueName, resetAttemptCount);

        var jobId = _backgroundJobProvider.Enqueue<IReprocessDeadLetterQueueCommandHandler>(
            h => h.ExecuteAsync(command, CancellationToken.None));

        _logger.LogInformation(
            "DLQ reprocess job enqueued. JobId={JobId}, Count={Count}, QueueName={QueueName}, ResetAttemptCount={Reset}",
            jobId, count, queueName ?? "(default)", resetAttemptCount);

        return Accepted(new { JobId = jobId, Count = count, QueueName = queueName ?? "(resolved from config at execution time)" });
    }
}

