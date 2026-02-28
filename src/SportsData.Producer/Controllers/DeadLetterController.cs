using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Extensions;
using SportsData.Producer.Application.Documents.Commands.ReprocessDeadLetterQueue;

namespace SportsData.Producer.Controllers;

/// <summary>
/// Endpoints for managing the dead-letter queue (DLQ).
/// </summary>
[ApiController]
[Route("api/dead-letter")]
public class DeadLetterController : ControllerBase
{
    private readonly IReprocessDeadLetterQueueCommandHandler _handler;
    private readonly ILogger<DeadLetterController> _logger;

    public DeadLetterController(
        IReprocessDeadLetterQueueCommandHandler handler,
        ILogger<DeadLetterController> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    /// <summary>
    /// Pulls messages from the RabbitMQ dead-letter queue and re-publishes them
    /// for normal processing.
    /// </summary>
    /// <param name="count">
    /// Maximum number of messages to reprocess. Defaults to 10.
    /// </param>
    /// <param name="queueName">
    /// Override the target DLQ name. Defaults to <c>document-created_error</c>
    /// (or the value of <c>SportsData.Producer:DeadLetterQueue:QueueName</c>
    /// in Azure AppConfig).
    /// </param>
    /// <param name="resetAttemptCount">
    /// When <c>true</c> (default), resets <c>AttemptCount</c> to 0 on each
    /// re-published message so the retry ladder starts fresh.
    /// </param>
    /// <param name="cancellationToken"></param>
    [HttpPost("reprocess")]
    public async Task<ActionResult<ReprocessDeadLetterQueueResult>> Reprocess(
        [FromQuery] int count = 10,
        [FromQuery] string? queueName = null,
        [FromQuery] bool resetAttemptCount = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "DLQ reprocess requested. Count={Count}, QueueName={QueueName}, ResetAttemptCount={Reset}",
            count, queueName ?? "(default)", resetAttemptCount);

        var command = new ReprocessDeadLetterQueueCommand(count, queueName, resetAttemptCount);
        var result = await _handler.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult();
    }
}
