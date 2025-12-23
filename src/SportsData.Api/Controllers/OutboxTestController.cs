using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events;

namespace SportsData.Api.Controllers;

/// <summary>
/// Test controller to validate MassTransit EF Core outbox pattern in API service.
/// API has a single AppDataContext, so outbox should work without OutboxPing hack.
/// </summary>
[ApiController]
[Route("api/test/outbox")]
public class OutboxTestController : ControllerBase
{
    private readonly AppDataContext _db;
    private readonly IEventBus _bus;
    private readonly ILogger<OutboxTestController> _logger;

    public OutboxTestController(
        AppDataContext db,
        IEventBus bus,
        ILogger<OutboxTestController> logger)
    {
        _db = db;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>
    /// Test 1: Publish event WITHOUT any entity changes.
    /// Should work WITHOUT OutboxPing since API has single DbContext.
    /// </summary>
    [HttpPost("publish-no-entity-changes")]
    public async Task<IActionResult> PublishWithoutEntityChanges()
    {
        var testId = Guid.NewGuid();
        
        _logger.LogInformation("TEST START: Publishing from AppDataContext (no entity changes). TestId={TestId}", testId);

        await _bus.Publish(new OutboxTestEvent(
            Message: "Test from API AppDataContext - NO entity changes",
            ContextType: nameof(AppDataContext),
            TestId: testId,
            PublishedUtc: DateTime.UtcNow
        ));

        await _db.SaveChangesAsync();

        _logger.LogInformation("TEST COMPLETE: SaveChanges called. TestId={TestId}", testId);

        return Ok(new
        {
            testId,
            message = "Event published via AppDataContext outbox (no entity changes)",
            timestamp = DateTime.UtcNow,
            note = "Check OutboxMessage table to verify event was stored"
        });
    }

    /// <summary>
    /// Test 2: Verify outbox is being used (not bypassed).
    /// </summary>
    [HttpPost("verify-outbox-used")]
    public async Task<IActionResult> VerifyOutboxIsUsed()
    {
        var testId = Guid.NewGuid();
        
        _logger.LogInformation("TEST START: Verifying outbox is used. TestId={TestId}", testId);

        var countBefore = await _db.OutboxMessages.CountAsync();

        await _bus.Publish(new OutboxTestEvent(
            Message: "Test verifying outbox is used",
            ContextType: nameof(AppDataContext),
            TestId: testId,
            PublishedUtc: DateTime.UtcNow
        ));

        var countAfterPublish = await _db.OutboxMessages.CountAsync();
        var inOutboxBeforeSave = countAfterPublish > countBefore;

        await _db.SaveChangesAsync();

        var countAfterSave = await _db.OutboxMessages.CountAsync();
        var inOutboxAfterSave = countAfterSave > countBefore;

        _logger.LogInformation(
            "TEST COMPLETE: Before={Before}, AfterPublish={AfterPublish}, AfterSave={AfterSave}", 
            countBefore, countAfterPublish, countAfterSave);

        return Ok(new
        {
            testId,
            countBefore,
            countAfterPublish,
            countAfterSave,
            inOutboxBeforeSave,
            inOutboxAfterSave,
            verdict = inOutboxAfterSave 
                ? "✅ PASS - Event went through outbox" 
                : "❌ FAIL - Event bypassed outbox",
            immediatePublish = inOutboxBeforeSave
                ? "⚠️ Event added to outbox immediately (before SaveChanges)"
                : "✅ Event NOT in outbox until SaveChanges called"
        });
    }

    /// <summary>
    /// Get outbox messages.
    /// </summary>
    [HttpGet("outbox-messages")]
    public async Task<IActionResult> GetOutboxMessages()
    {
        var messages = await _db.OutboxMessages
            .OrderByDescending(m => m.SentTime)
            .Take(50)
            .Select(m => new
            {
                m.SequenceNumber,
                m.MessageType,
                m.SentTime,
                m.EnqueueTime,
                MessageBody = m.Body.Length > 200 ? m.Body.Substring(0, 200) + "..." : m.Body
            })
            .ToListAsync();

        return Ok(new { count = messages.Count, messages });
    }
}
