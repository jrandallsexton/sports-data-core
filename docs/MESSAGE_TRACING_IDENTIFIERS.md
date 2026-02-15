# Message Tracing Identifiers

## Overview

This document describes the three identifiers used for distributed tracing and message flow observability in the SportsData platform.

## The Three Identifiers

### 1. CorrelationId (Guid)
**Purpose**: Tracks a business operation across all services and messages

**Source**: OpenTelemetry W3C Trace Context (`Activity.Current.TraceId`)

**Lifetime**: Entire business operation from start to finish

**Example Flow**:
```
User initiates "Source 2024 Season" → CorrelationId: 0af76519-16cd-43dd-8448-eb211c80319c
  ├─ Season document sourced
  ├─ 50 TeamSeason documents sourced  
  ├─ 1000 AthleteSeason documents sourced
  └─ All share same CorrelationId
```

**How to get it**:
```csharp
var correlationId = ActivityExtensions.GetCorrelationId();
```

**When it flows**:
- Through HTTP headers (traceparent)
- Through all events/messages as a property
- Through all log scopes

---

### 2. CausationId (Guid)
**Purpose**: Identifies the parent message that caused this message to be published

**Source**: The MessageId of the message being processed

**Lifetime**: One hop in the message chain

**Example Flow**:
```
DocumentCreated (MessageId: AAA, CausationId: null)
  └─ triggers processor
      └─ publishes DocumentRequested (MessageId: BBB, CausationId: AAA)
          └─ triggers provider
              └─ publishes DocumentCreated (MessageId: CCC, CausationId: BBB)
```

**How to set it**:
```csharp
// For the first message in a chain (no parent):
var causationId = ActivityExtensions.GetSpanBasedCausationId();

// For child messages (has a parent):
var causationId = parentMessage.MessageId;
```

---

### 3. MessageId (Guid)
**Purpose**: Unique identifier for this specific message instance

**Source**: `Guid.NewGuid()` when publishing

**Lifetime**: Single message

**Usage**: Becomes the CausationId for any child messages this message triggers

**How to generate**:
```csharp
var messageId = ActivityExtensions.GenerateMessageId(); // or Guid.NewGuid()
```

---

## Usage Patterns

### Publishing a Root Message (No Parent)
```csharp
var messageId = Guid.NewGuid();
var correlationId = ActivityExtensions.GetCorrelationId();
var causationId = ActivityExtensions.GetSpanBasedCausationId(); // from OpenTelemetry span

var evt = new DocumentCreated(
    // ... other properties
    CorrelationId: correlationId,
    CausationId: causationId,      // First message - use SpanId
    MessageId: messageId
);

await _eventBus.Publish(evt);
```

### Publishing a Child Message (Has Parent)
```csharp
// Inside a message handler - processing parentMessage
var messageId = Guid.NewGuid();
var correlationId = parentMessage.CorrelationId;  // Flow from parent
var causationId = parentMessage.MessageId;        // Parent's MessageId

var evt = new DocumentRequested(
    // ... other properties
    CorrelationId: correlationId,  // Same business operation
    CausationId: causationId,      // Parent's MessageId
    MessageId: messageId           // New unique ID
);

await _eventBus.Publish(evt);
```

---

## Querying in Seq

### Find all messages in a business operation:
```sql
CorrelationId = "0af76519-16cd-43dd-8448-eb211c80319c"
```

### Find all messages caused by a specific message:
```sql
CausationId = "parent-message-id-here"
```

### Find a specific message:
```sql
MessageId = "specific-message-id-here"
```

### Build message chain (parent → child):
```sql
-- Start with a message
MessageId = "AAA"

-- Find children
CausationId = "AAA"

-- Find grandchildren
CausationId IN (messages where CausationId = "AAA").MessageId
```

---

## Benefits

1. **End-to-end operation tracking** via CorrelationId
2. **Message lineage** via CausationId → MessageId relationships
3. **Retry diagnosis** - see which specific message instance failed
4. **Dependency tracking** - see what triggered what
5. **Performance analysis** - measure time between parent and child messages

---

## Migration Notes

### Breaking Changes
- `EventBase` now requires `MessageId` parameter
- All event constructors must provide MessageId
- CausationId semantics changed from "processor type" to "parent message"

### Backward Compatibility
- Existing `CausationId` static GUIDs can still be logged separately as "ProcessorType"
- No changes to CorrelationId behavior (still W3C Trace Context)

---

## Example: Document Processing Flow

```
1. ResourceIndexJob publishes DocumentCreated
   CorrelationId: 0af76519... (from TraceId)
   CausationId:   12ab34cd... (from SpanId)
   MessageId:     AAA-111

2. DocumentCreatedHandler processes AAA-111, publishes to Hangfire
   (No new message, just scheduling)

3. DocumentProcessor processes document, finds missing dependency
   Publishes DocumentRequested:
   CorrelationId: 0af76519... (same - still same operation)
   CausationId:   AAA-111    (parent was DocumentCreated)
   MessageId:     BBB-222

4. Provider fetches document, publishes DocumentCreated:
   CorrelationId: 0af76519... (same)
   CausationId:   BBB-222    (parent was DocumentRequested)
   MessageId:     CCC-333

5. DocumentCreatedHandler retries original, now succeeds
   (Processes CCC-333)
```

Query to see this flow in Seq:
```sql
CorrelationId = "0af76519..." 
| order by CreatedUtc
| project MessageId, CausationId, EventType, CreatedUtc
```

Result:
```
MessageId   CausationId   EventType           CreatedUtc
AAA-111     12ab34cd...   DocumentCreated     10:00:00
BBB-222     AAA-111       DocumentRequested   10:00:01
CCC-333     BBB-222       DocumentCreated     10:00:02
```

---

## Practical Query Patterns for Debugging

### Most Common: Find Direct Children of a Message
When debugging a specific message failure, find what it spawned:

```sql
-- I have a message that failed - what did it trigger?
MessageId = "AAA-111" OR CausationId = "AAA-111"
```

This shows the original message AND everything it directly caused.

### Trace Message Chain Forward (Parent → Children)
```sql
-- Start with a specific message
MessageId = "AAA-111"

-- Then find its children (new query)
CausationId = "AAA-111"

-- Then find grandchildren (new query with child MessageIds)
CausationId IN ("BBB-222", "BBB-223", "BBB-224")
```

### Trace Message Chain Backward (Child → Parent)
```sql
-- I have a message - what caused it?
MessageId = "CCC-333"
-- Look at CausationId field: "BBB-222"

-- Find the parent (new query)
MessageId = "BBB-222"
-- Look at CausationId field: "AAA-111"

-- Find the grandparent (new query)
MessageId = "AAA-111"
```

### Find All Retries of a Specific Document
```sql
SourceUrlHash = "specific-hash-here"
| order by AttemptCount, CreatedUtc
```

### Find All Failed Messages in an Operation (Use Sparingly)
```sql
-- Only use CorrelationId for small operations or with additional filters
CorrelationId = "0af76519..." AND AttemptCount >= 10
```

**Warning**: Querying by CorrelationId alone in historical sourcing operations will return thousands of results. Always add additional filters (AttemptCount, DocumentType, specific time range).

### Better Approach for Large Operations
Instead of querying the entire CorrelationId, start with known failures:

```sql
-- Find documents that hit max retries
AttemptCount >= 10 AND CreatedUtc > @Today
| project MessageId, DocumentType, SourceUrlHash, RetryReason

-- Then for each interesting failure, trace its lineage:
MessageId = "failed-message-id" OR CausationId = "failed-message-id"
```

---

## Additional Resources

- OpenTelemetry W3C Trace Context: https://www.w3.org/TR/trace-context/
- Distributed Tracing Patterns: https://microservices.io/patterns/observability/distributed-tracing.html
- Message Causality: https://www.enterpriseintegrationpatterns.com/patterns/messaging/
