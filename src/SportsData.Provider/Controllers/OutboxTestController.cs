using Microsoft.AspNetCore.Mvc;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;

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
    /// COMPREHENSIVE TEST: Mimics Provider's actual document sourcing flow.
    /// Provider sources documents from external APIs, stores in blob, then publishes DocumentCreated events.
    /// This test validates the complete event publishing pipeline.
    /// </summary>
    [HttpPost("comprehensive-test")]
    public async Task<IActionResult> ComprehensiveTest()
    {
        var testId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();
        
        _logger.LogInformation(
            "PROVIDER TEST START: Simulating document sourcing and event publishing. TestId={TestId}, CorrelationId={CorrelationId}", 
            testId, 
            correlationId);

        try
        {
            // Simulate what Provider does in PublishDocumentEventsProcessor
            // 1. Provider sources document from external API (ESPN, etc.)
            // 2. Provider stores document JSON in blob storage
            // 3. Provider publishes DocumentCreated event to notify Producer

            // Minimal test JSON for OutboxTestDocumentProcessor
            // This prevents DocumentCreatedProcessor from trying to fetch from Provider
            var testJson = $$"""
            {
                "$ref": "http://test.sportdeets.com/outbox-test/{{testId}}",
                "id": "{{testId}}",
                "name": "OutboxTest",
                "testData": {
                    "message": "Test document from Provider",
                    "timestamp": "{{DateTime.UtcNow:O}}",
                    "correlationId": "{{correlationId}}",
                    "purpose": "Validate Provider → Producer → DocumentCreatedHandler → Hangfire → OutboxTestDocumentProcessor flow"
                }
            }
            """;

            var documentCreatedEvent = new DocumentCreated(
                Id: testId.ToString(),
                ParentId: testId.ToString(), // Use testId as ParentId for test processor
                Name: "ProviderTest_OutboxTest",
                Ref: new Uri($"https://test.sportdeets.com/documents/{testId}"),
                SourceRef: new Uri($"https://test.sportdeets.com/outbox-test/{testId}"),
                DocumentJson: testJson, // ✅ Include JSON so Producer doesn't try to fetch from Provider
                SourceUrlHash: $"test-hash-{testId}",
                Sport: Sport.FootballNcaa,
                SeasonYear: 2025,
                DocumentType: DocumentType.OutboxTest,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: correlationId,
                CausationId: causationId,
                AttemptCount: 0,
                IncludeLinkedDocumentTypes: null
            );

            _logger.LogInformation(
                "Publishing DocumentCreated event: DocumentType={DocumentType}, Sport={Sport}, Provider={Provider}, HasJson={HasJson}",
                documentCreatedEvent.DocumentType,
                documentCreatedEvent.Sport,
                documentCreatedEvent.SourceDataProvider,
                !string.IsNullOrEmpty(documentCreatedEvent.DocumentJson));

            // Publish the event (goes directly to Azure Service Bus - NO outbox)
            await _bus.Publish(documentCreatedEvent);

            _logger.LogInformation("PROVIDER TEST COMPLETE: DocumentCreated event published successfully");

            return Ok(new
            {
                testId,
                correlationId,
                success = true,
                verdict = "✅ Event published to Azure Service Bus",
                eventDetails = new
                {
                    eventType = nameof(DocumentCreated),
                    documentType = documentCreatedEvent.DocumentType.ToString(),
                    sport = documentCreatedEvent.Sport.ToString(),
                    provider = documentCreatedEvent.SourceDataProvider.ToString(),
                    seasonYear = documentCreatedEvent.SeasonYear,
                    correlationId,
                    causationId,
                    hasDocumentJson = !string.IsNullOrEmpty(documentCreatedEvent.DocumentJson),
                    documentJsonLength = documentCreatedEvent.DocumentJson?.Length ?? 0
                },
                publishingStrategy = "Direct publish to Azure Service Bus (NO outbox pattern)",
                flow = "Provider → Azure Service Bus → Producer (listens for DocumentCreated)",
                expectedDownstream = new[]
                {
                    "1. Producer DocumentCreatedHandler receives event",
                    "2. Producer queues Hangfire job (look for HangfireJobId in logs)",
                    "3. Hangfire executes DocumentCreatedProcessor",
                    "4. DocumentProcessorFactory selects OutboxTestDocumentProcessor",
                    "5. OutboxTestDocumentProcessor processes test document",
                    "6. OutboxTestEvent published via outbox",
                    "7. Check Producer's OutboxMessages table for results"
                },
                testJson,
                note = "This test includes DocumentJson so Producer won't try to fetch from Provider blob storage",
                troubleshooting = new
                {
                    checkProducerLogs = "Search for '🔔 HANDLER_ENTRY' and 'OutboxTest' in Producer logs",
                    checkHangfireDashboard = "Navigate to Producer /dashboard to see job status",
                    checkOutboxTable = "Query Producer.FootballDataContext.OutboxMessages for OutboxTestEvent",
                    expectedLogSequence = new[]
                    {
                        "Provider: PROVIDER TEST START",
                        "Provider: Publishing DocumentCreated event",
                        "Producer: 🔔 HANDLER_ENTRY (DocumentCreatedHandler)",
                        "Producer: ✅ HANDLER_ENQUEUED (HangfireJobId logged)",
                        "Producer: 🏁 HANDLER_EXIT",
                        "Producer: 🚀 PROCESSOR_ENTRY (DocumentCreatedProcessor)",
                        "Producer: ✅ PROCESSOR_DOCUMENT_OBTAINED",
                        "Producer: ✅ PROCESSOR_FOUND (OutboxTestDocumentProcessor)",
                        "Producer: ⚙️ PROCESSOR_EXECUTE",
                        "Producer: TEST PROCESSOR (BaseDataContext): Processing...",
                        "Producer: ✅ PROCESSOR_COMPLETED"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PROVIDER TEST FAILED: Error publishing DocumentCreated event");
            
            return StatusCode(500, new
            {
                testId,
                success = false,
                verdict = "❌ Failed to publish event",
                error = ex.Message,
                stackTrace = ex.StackTrace,
                troubleshooting = "Check Azure Service Bus connection string and permissions"
            });
        }
    }

    /// <summary>
    /// Test: Publish simple test event directly (no outbox in Provider).
    /// Provider publishes immediately to Azure Service Bus.
    /// </summary>
    [HttpPost("publish-direct")]
    public async Task<IActionResult> PublishDirect()
    {
        var testId = Guid.NewGuid();
        
        _logger.LogInformation("TEST START: Publishing directly from Provider (no outbox). TestId={TestId}", testId);

        try
        {
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
                success = true,
                message = "Event published DIRECTLY to Azure Service Bus (Provider has no outbox)",
                timestamp = DateTime.UtcNow,
                note = "Provider does not use outbox pattern - events are sent immediately"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TEST FAILED: Error publishing OutboxTestEvent. TestId={TestId}", testId);
            
            return StatusCode(500, new
            {
                testId,
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Test: Publish multiple DocumentCreated events in batch (simulates bulk sourcing).
    /// </summary>
    [HttpPost("publish-batch")]
    public async Task<IActionResult> PublishBatch([FromQuery] int count = 5)
    {
        if (count < 1 || count > 100)
        {
            return BadRequest("Count must be between 1 and 100");
        }

        var batchId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        
        _logger.LogInformation(
            "BATCH TEST START: Publishing {Count} DocumentCreated events. BatchId={BatchId}", 
            count, 
            batchId);

        try
        {
            var publishTasks = new List<Task>();
            var eventIds = new List<Guid>();

            for (int i = 0; i < count; i++)
            {
                var eventId = Guid.NewGuid();
                eventIds.Add(eventId);

                var evt = new DocumentCreated(
                    Id: eventId.ToString(),
                    ParentId: batchId.ToString(),
                    Name: $"ProviderBatchTest_{i}",
                    Ref: new Uri($"https://test.sportdeets.com/documents/{eventId}"),
                    SourceRef: new Uri($"https://site.api.espn.com/test/batch/{i}"),
                    DocumentJson: null,
                    SourceUrlHash: $"batch-hash-{batchId}-{i}",
                    Sport: Sport.FootballNcaa,
                    SeasonYear: 2025,
                    DocumentType: DocumentType.Event,
                    SourceDataProvider: SourceDataProvider.Espn,
                    CorrelationId: correlationId,
                    CausationId: Guid.NewGuid(),
                    AttemptCount: 0,
                    IncludeLinkedDocumentTypes: null
                );

                publishTasks.Add(_bus.Publish(evt));
            }

            await Task.WhenAll(publishTasks);

            _logger.LogInformation(
                "BATCH TEST COMPLETE: Published {Count} events. BatchId={BatchId}", 
                count, 
                batchId);

            return Ok(new
            {
                batchId,
                correlationId,
                success = true,
                eventsPublished = count,
                eventIds,
                verdict = $"✅ {count} events published to Azure Service Bus",
                note = "Check Producer service logs to confirm all events were received and processed"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BATCH TEST FAILED: Error publishing batch. BatchId={BatchId}", batchId);
            
            return StatusCode(500, new
            {
                batchId,
                success = false,
                error = ex.Message
            });
        }
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
            workflow = new[]
            {
                "1. Provider sources documents from external APIs (ESPN, CBS, Yahoo, etc.)",
                "2. Provider stores raw JSON in Azure Blob Storage",
                "3. Provider publishes DocumentCreated event to Azure Service Bus",
                "4. Producer receives event and processes document asynchronously"
            },
            eventTypes = new[]
            {
                "DocumentCreated - Published when a new document is sourced",
                "ResourceIndexed - Published when resource indexing completes"
            },
            note = "Provider is stateless and does not maintain a database, hence no outbox pattern is needed.",
            architecture = new
            {
                dataStore = "Azure Blob Storage (JSON documents)",
                messaging = "Azure Service Bus (direct publish)",
                consumers = new[] { "Producer", "Other downstream services" }
            }
        });
    }
}
