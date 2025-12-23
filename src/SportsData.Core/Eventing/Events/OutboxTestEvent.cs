using System;

namespace SportsData.Core.Eventing.Events;

/// <summary>
/// Test event used to validate MassTransit outbox pattern works correctly
/// across different DbContext types without requiring entity changes.
/// </summary>
/// <param name="Message">Test message content</param>
/// <param name="ContextType">The type of DbContext used to publish this event</param>
/// <param name="TestId">Unique identifier for this test</param>
/// <param name="PublishedUtc">When the event was published</param>
public record OutboxTestEvent(
    string Message,
    string ContextType,
    Guid TestId,
    DateTime PublishedUtc
);
