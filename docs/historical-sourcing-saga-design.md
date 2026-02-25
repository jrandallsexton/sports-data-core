# Historical Season Sourcing - Event-Driven Saga Design

## Status
**Draft - Design Phase**  
Date: February 20, 2026

## Overview

Replace time-based tier delays with event-driven progression using MassTransit Sagas. Provider saga orchestrates tier progression based on actual processing completion signals from Producer.

## Core Concept: Completion Flag Approach

Instead of polling or publishing thousands of events, use a **selective flag** on DocumentCreated events to signal when Provider should be notified of completion.

### Flow Summary

1. **Provider (ResourceIndexJob)** - While processing a tier:
   - Knows total document count (ResourceIndexItems.Count)
   - Determines threshold point (e.g., 95% processed)
   - Sets `NotifyOnCompletion = true` for last 5-10% of documents
   - Publishes DocumentCreated events to Producer

2. **Producer (DocumentProcessorBase)** - After processing document:
   - Checks `NotifyOnCompletion` flag on ProcessDocumentCommand
   - If true AND processing succeeded, publishes `DocumentProcessingCompleted` event
   - Event flows back to Provider saga

3. **Provider Saga** - Orchestrating tier progression:
   - Waits in current tier state (e.g., "WaitingForSeasonCompletion")
   - Counts incoming `DocumentProcessingCompleted` events
   - When threshold reached (e.g., 3 events) → starts next tier
   - Saga remains in current state indefinitely if threshold is not reached (no timeout fallback)

## Architecture Components

### 1. DocumentCreated Event Enhancement

**Add property:**
```csharp
bool NotifyOnCompletion { get; init; }
```

**When Provider sets to true:**
- Last 5% of documents OR minimum 1 document (whichever is MORE)
- Formula: `Math.Max(1, ceiling(totalDocs * 0.05))`
- Example: 130 venues → flag last 7 documents (5%)
- Example: 2 seasons → flag last 1 document (minimum threshold)
- Example: 1 season → flag last 1 document (edge case)

### 2. ProcessDocumentCommand Enhancement

**Add property:**
```csharp
bool NotifyOnCompletion { get; init; }
```

**Populated from:** DocumentCreated.NotifyOnCompletion when command is instantiated in DocumentCreatedHandler

### 3. DocumentProcessorBase Integration

**Key insight:** Place completion notification logic in `ProcessAsync` method (the entry point for all document processors).

**Location:** After successful `ProcessInternal()` completion, before final log statement

**Logic:**
```csharp
public virtual async Task ProcessAsync(ProcessDocumentCommand command)
{
    using (_logger.BeginScope(command.ToLogScope()))
    {
        try
        {
            await ProcessInternal(command);
            
            // NEW: Check if Provider wants completion notification
            if (command.NotifyOnCompletion)
            {
                await PublishCompletionNotification(command);
            }
            
            _logger.LogInformation("{ProcessorName} completed.", GetType().Name);
        }
        catch (ExternalDocumentNotSourcedException retryEx)
        {
            // Existing retry logic...
        }
    }
}
```

**Benefits of this approach:**
- ✅ Centralized - all processors get this behavior automatically
- ✅ Happens after successful processing only
- ✅ No changes needed in derived processors
- ✅ Respects the command flag without interpretation

### 4. New Event: DocumentProcessingCompleted

**Published by:** Producer (DocumentProcessorBase)  
**Consumed by:** Provider Saga

```csharp
public record DocumentProcessingCompleted(
    Guid CorrelationId,
    DocumentType DocumentType,
    string SourceUrlHash,
    DateTimeOffset CompletedUtc,
    Sport Sport,
    int? SeasonYear
);
```

### 5. MassTransit Saga

**States:**
- NotStarted
- WaitingForSeasonCompletion
- WaitingForVenueCompletion
- WaitingForTeamSeasonCompletion
- WaitingForAthleteSeasonCompletion
- Completed

**State Data:**
```csharp
public class HistoricalSeasonSourcingState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    
    public Sport Sport { get; set; }
    public int SeasonYear { get; set; }
    public SourceDataProvider Provider { get; set; }
    
    // Tier completion tracking
    public int SeasonCompletionEventsReceived { get; set; }
    public int VenueCompletionEventsReceived { get; set; }
    public int TeamSeasonCompletionEventsReceived { get; set; }
    public int AthleteSeasonCompletionEventsReceived { get; set; }
    
    // Timestamps
    public DateTime StartedUtc { get; set; }
    public DateTime? SeasonCompletedUtc { get; set; }
    public DateTime? VenueCompletedUtc { get; set; }
    public DateTime? TeamSeasonCompletedUtc { get; set; }
    public DateTime? AthleteSeasonCompletedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}
```

## Open Design Questions

### A. Completion Threshold Strategy

**Question:** How many completion events should saga wait for before transitioning to next tier?

**Options:**
1. **Fixed count** (e.g., "wait for 3 events")
   - Pros: Simple, predictable
   - Cons: Doesn't scale with tier size
   
2. **Percentage of flagged docs** (e.g., "wait for 70% of flagged docs")
   - Pros: Scales with tier size
   - Cons: Saga needs to know how many docs were flagged
   
3. **First signal** (e.g., "wait for ANY completion event")
   - Pros: Fastest progression
   - Cons: Risky - might start too early

**Current recommendation:** Fixed count of 3 events (simple, fast enough, safe)

**Decision:** ✅ **Upon receipt of ANY completion event**
- Maximally aggressive - fastest possible progression
- Simple - no counting, no thresholds
- Safety: 5% flag density ensures ~95% of tier complete before first signal
- First completion = tier is wrapping up

---

### B. Timeout Per Tier

**Question:** Should saga have a maximum wait time before forcing progression to next tier?

**Purpose:** Prevent stalling if flagged documents fail or processing is stuck

**Options:**
1. **No timeout** - Wait indefinitely for threshold
   - Risk: Could stall entire sourcing run
   
2. **Per-tier timeout** (e.g., 15 min per tier)
   - Pros: Guarantees forward progress
   - Cons: Might start next tier before ready
   
3. **Total sourcing run timeout** (e.g., 8 hours max)
   - Pros: Allows variable tier times
   - Cons: Later tiers might be rushed

**Current recommendation:** 15 minute timeout per tier with warning log

**Decision:** ✅ **No timeout - Never force-advance**
- Conservative approach - won't start tier prematurely
- Clear failure signal - if saga stalls, something is broken
- Rationale: Backlog or errors should be fixed, not hidden by auto-advancing
- **Observability Requirements:**
  - Alert if saga in same state > 30 minutes
  - Manual intervention API to force-advance if needed for operations
  - Runbook: "If saga stalled, check Hangfire/Seq for failed processors"

---

### C. Flag Density Configuration

**Question:** What percentage/count of documents should be flagged?

**Considerations:**
- Too few flags: Risk if those specific documents fail
- Too many flags: Unnecessary event volume
- Variable tier sizes: Need both percentage and minimum

**Options:**
1. **Percentage only** (e.g., "last 5%")
   - Problem: Small tiers (2 seasons) would flag 0.1 docs
   
2. **Fixed count only** (e.g., "last 10 documents")
   - Problem: Large tiers (8000 athletes) would flag too few
   
3. **Hybrid: percentage with minimum** (e.g., "last 5% OR minimum 5 docs, whichever is MORE")
   - Pros: Scales well across tier sizes
   - Examples:
     - 1 Season → flag 5 docs (minimum)
     - 130 Venues → flag 7 docs (5% = 6.5, round up)
     - 8000 Athletes → flag 400 docs (5%)

**Current recommendation:** 5% with minimum of 5 documents

**Decision:** ✅ **5% with Math.Max(1, ceiling) for edge cases**
- Formula: `Math.Max(1, (int)Math.Ceiling(totalDocs * 0.05))`
- Handles edge case: 1 document tier → flags 1 doc (not 0)
- Examples:
  - 1 doc: Max(1, Ceiling(0.05)) = 1 doc flagged ✅
  - 20 docs: Max(1, Ceiling(1.0)) = 1 doc flagged ✅
  - 130 docs: Max(1, Ceiling(6.5)) = 7 docs flagged ✅
  - 8000 docs: Max(1, Ceiling(400)) = 400 docs flagged ✅
- Guarantees at least 1 completion event per tier

---

### D. Failure Semantics

**Question:** What should saga do when receiving completion events from documents that ultimately failed processing?

**Considerations:**
- NotifyOnCompletion flag only sent on successful processing
- But documents could fail with ExternalDocumentNotSourcedException (retry)
- Retry might succeed later or exhaust attempts

**Options:**
1. **Ignore failures** - Only successful completions count
   - Saga only counts DocumentProcessingCompleted (success signals)
   - Failed docs don't send notification
   
2. **Track failures separately** - Consider failure rate
   - Add FailureNotification event type
   - Saga tracks both success and failure
   - Don't progress if failure rate too high

**Current recommendation:** Ignore failures (Option 1) - simpler, relies on retry mechanism

**Decision:** ✅ **Only successful processing sends completion events**
- Architecture guarantee: DocumentProcessorBase.ProcessAsync only publishes notification if ProcessInternal succeeds
- If ProcessInternal throws exception, notification code is never reached
- No ambiguity - failures naturally don't trigger progression
- No additional handling needed

---

### E. Saga Initial Trigger

**Question:** How does saga get started?

**Options:**
1. **HistoricalSeasonSourcingService publishes event** after creating ResourceIndex records
   ```csharp
   await _publishEndpoint.Publish(new SeasonSourcingStarted(correlationId, sport, provider, year));
   ```
   
2. **Saga subscribes to ResourceIndex creation** (database trigger or change feed)
   
3. **HistoricalSeasonSourcingService calls saga directly** via ISagaRepository

**Current recommendation:** Option 1 (explicit event) - clearest intent

**Decision:** ✅ **Manual API invocation (existing pattern)**
- Keep existing POST /api/sourcing/historical/seasons endpoint
- HistoricalSeasonSourcingService publishes `SeasonSourcingStarted` event after creating ResourceIndex
- Saga subscribes to this event for Initial → WaitingForSeasonCompletion transition
- Familiar UX, explicit user intent, auditable via API logs

---

## Implementation Plan

### Phase 1: Core Infrastructure ✅ COMPLETED
- [x] Add `NotifyOnCompletion` to DocumentCreated event
- [x] Add `NotifyOnCompletion` to ProcessDocumentCommand
- [x] Update DocumentCreatedProcessor to copy flag from event to command
- [x] Create `DocumentProcessingCompleted` event
- [x] Add completion notification logic to DocumentProcessorBase.ProcessAsync
- [x] Update ProcessDocumentCommandExtensions.ToDocumentCreated to copy flag
- [x] All existing tests passing (8/8 DocumentProcessorBaseTests)

**Verification:**
- ✅ Core builds successfully
- ✅ Producer builds successfully
- ✅ All DocumentProcessorBase tests pass
- ✅ Flag flows: DocumentCreated → ProcessDocumentCommand → ProcessAsync
- ✅ Notification published after successful ProcessInternal only

### Phase 2: Provider Saga ✅ COMPLETED
- [x] Create HistoricalSeasonSourcingState entity with EF configuration
- [x] Create HistoricalSeasonSourcingSaga state machine with 4 tiers
- [x] Configure MassTransit saga repository (PostgreSQL via EntityFramework)
- [x] Create database migration for saga state table
- [x] Add saga consumer registration in Provider startup
- [x] Create TriggerTierSourcingConsumer to execute ResourceIndexJob when saga triggers next tier
- [x] Add TriggerTierSourcing consumer registration

**Verification:**
- ✅ Provider builds successfully
- ✅ Saga state machine transitions: NotStarted → WaitingForSeasonCompletion → WaitingForVenueCompletion → WaitingForTeamSeasonCompletion → WaitingForAthleteSeasonCompletion → Completed
- ✅ Each tier publishes TriggerTierSourcing event to start next tier's ResourceIndexJob
- ✅ Saga persists state to HistoricalSourcingSagas table for crash recovery

### Phase 3: ResourceIndexJob Integration ✅ COMPLETED
- [x] Update ResourceIndexJob to calculate flag threshold using Math.Max(1, ceiling(totalDocs * 0.05))
- [x] Track document position during enumeration
- [x] Set NotifyOnCompletion flag on last N% of documents
- [x] Add NotifyOnCompletion parameter to ProcessResourceIndexItemCommand
- [x] Update ResourceIndexItemProcessor to pass flag to DocumentCreated events (all 3 paths: new, updated, cached)
- [x] Inject HistoricalSourcingConfig into ResourceIndexJob for saga configuration

**Verification:**
- ✅ Provider builds successfully
- ✅ Flag calculation logs: TotalDocuments, FlagThreshold, FlagStartPosition, FlagPercentage
- ✅ Flag propagates: ResourceIndexJob → ProcessResourceIndexItemCommand → DocumentCreated → ProcessDocumentCommand → DocumentProcessorBase
- ✅ All existing Producer tests pass (8/8 DocumentProcessorBaseTests)

### Phase 4: Configuration & Testing
- [ ] Add saga configuration (thresholds, timeouts)
- [ ] Add comprehensive logging for saga transitions
- [ ] Create integration tests for saga state machine
- [ ] Create integration tests for flag propagation
- [ ] Test timeout fallback behavior
- [ ] Test failure recovery scenarios

### Phase 5: Observability
- [ ] Add Grafana dashboard for saga state
- [ ] Add metrics for tier progression timing
- [ ] Add alerts for stalled sagas
- [ ] Document saga visualization in Seq

## Configuration Structure

```json
{
  "HistoricalSourcing": {
    "SagaConfig": {
      "CompletionThreshold": 1,
      "FlagPercentage": 0.05,
      "MinimumFlaggedDocuments": 1,
      "TierTimeoutMinutes": null,
      "AlertAfterMinutes": 30
    }
  }
}
```

**Configuration Notes:**
- `CompletionThreshold`: 1 = progress on ANY completion event
- `FlagPercentage`: 0.05 = flag last 5% of documents
- `MinimumFlaggedDocuments`: 1 = ensure at least 1 doc flagged (edge case protection)
- `TierTimeoutMinutes`: null = no automatic timeout
- `AlertAfterMinutes`: 30 = observability alert if saga stalled

## Benefits Summary

### vs. Time-Based Delays
- ✅ No guessing delays
- ✅ Adapts to actual processing speed
- ✅ Self-tuning as performance changes
- ✅ Starts next tier as soon as ready
- ✅ No wasted waiting time

### vs. Polling Stats Endpoint
- ✅ No periodic HTTP requests
- ✅ Pure event-driven architecture
- ✅ Minimal event volume (3-10 per tier vs 1000s)
- ✅ No cross-service query complexity

### vs. Publishing Every Completion
- ✅ 99%+ fewer events (3-10 vs 1000s)
- ✅ Message bus not overwhelmed
- ✅ Saga state smaller (just counter, not tracking all docs)

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Flagged documents fail | **Decision D**: Only successful processing sends completion events; architecture guarantees no false positives |
| Saga state corruption | MassTransit handles persistence/recovery via PostgreSQL |
| Too early progression | **Decision A**: ANY completion event triggers (threshold=1) with 5% flag density provides sufficient signal |
| Too late progression | **Decision B**: No timeout - failures require explicit fixes; observability alerts if saga stalled >30 min |
| Event ordering issues | MassTransit correlation handles this |
| Learning curve | Start simple, iterate; comprehensive design doc and logging |

## Next Steps

**Phases 1-3 Complete:** Core infrastructure, saga implementation, and ResourceIndexJob integration are finished, tested locally, and in PR review.

**Current Phase 4 Status:**
- ✅ Saga configuration implemented
- ✅ API endpoint created (`POST /api/sourcing/historical/seasons/saga`)
- ✅ Local smoke testing successful
- ✅ PR created and under review

**Remaining Work:**

1. **Complete PR review and merge** - Address any final feedback, merge to main
2. **Production deployment** - Apply migration, verify AppConfig, monitor first saga execution
3. **Phase 5: Observability** - Add Grafana dashboard, metrics, alerts for saga monitoring
4. **Documentation** - Update team docs with saga usage patterns and troubleshooting guide
5. **Performance validation** - Compare saga timing vs previous Hangfire approach

## References

- [MassTransit Saga Documentation](https://masstransit.io/documentation/patterns/saga)
- [Current HistoricalSeasonSourcingService](../src/SportsData.Provider/Application/Sourcing/Historical/HistoricalSeasonSourcingService.cs)
- [DocumentProcessorBase](../src/SportsData.Producer/Application/Documents/Processors/DocumentProcessorBase.cs)
- [DocumentCreated Event](../src/SportsData.Core/Eventing/Events/Documents/DocumentCreated.cs)

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-02-20 | Use completion flag approach | Balances event volume with responsiveness |
| 2026-02-20 | Place logic in DocumentProcessorBase.ProcessAsync | Centralizes behavior, no processor changes needed |
| 2026-02-20 | ANY completion event triggers progression | Maximally aggressive, 5% density provides safety |
| 2026-02-20 | No timeout on saga | Don't hide failures, require explicit fixes |
| 2026-02-20 | 5% flag density with Math.Max(1) | Handles edge cases, scales across tier sizes |
| 2026-02-20 | Only success sends events | Architecture guarantee via ProcessAsync flow |
| 2026-02-20 | Manual API start with event publication | Familiar pattern, clear intent |
