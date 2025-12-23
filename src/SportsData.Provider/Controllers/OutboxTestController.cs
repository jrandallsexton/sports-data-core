using Microsoft.AspNetCore.Mvc;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events;

namespace SportsData.Provider.Controllers;

/// <summary>
/// Test controller to validate event publishing in Provider service.
/// Provider does NOT use outbox - it publishes directly to Azure Service Bus.
/// </summary>
[ApiController]
[Route("api/test/outbox")]
public class OutboxTestController : ControllerBase
{
    private readonly IEventBus _bus;
    private readonly ILogger<OutboxTestController> _logger;

    public OutboxTestController(
        IEventBus bus,
        ILogger<OutboxTestController> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    /// <summary>
    /// Test: Publish event directly (no outbox in Provider).
    /// Provider publishes immediately to Azure Service Bus.
    /// </summary>
    [HttpPost("publish-direct")]
    public async Task<IActionResult> PublishDirect()
    {
        var testId = Guid.NewGuid();
        
        _logger.LogInformation("TEST START: Publishing directly from Provider (no outbox). TestId={TestId}", testId);

        await _bus.Publish(new OutboxTestEvent(
            Message: "Test from Provider - direct publish (NO outbox)",
            ContextType: "Provider (No DbContext)",
            TestId: testId,
            PublishedUtc: DateTime.UtcNow
        ));

        _logger.LogInformation("TEST COMPLETE: Event published directly. TestId={TestId}", testId);

        return Ok(new
        {
            testId,
            message = "Event published DIRECTLY to Azure Service Bus (Provider has no outbox)",
            timestamp = DateTime.UtcNow,
            note = "Provider does not use outbox pattern - events are sent immediately"
        });
    }

    /// <summary>
    /// Info endpoint explaining Provider's publishing behavior.
    /// </summary>
    [HttpGet("info")]
    public IActionResult GetInfo()
    {
        return Ok(new
        {
            service = "Provider",
            outboxEnabled = false,
            publishingStrategy = "Direct publish to Azure Service Bus",
            explanation = "Provider does not use outbox pattern. Events are published immediately when Publish() is called.",
            note = "This is intentional - Provider sources documents and publishes availability events without database transactions."
        });
    }
}
