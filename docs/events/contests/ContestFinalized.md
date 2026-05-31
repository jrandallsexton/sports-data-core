# ContestFinalized

Fired by Producer once the canonical Contest row has been enriched with final scores, winner, odds results, and `FinalizedUtc`. This is the trigger API consumes to kick off picks scoring — distinct from `ContestCompleted`, which fires the moment STATUS_FINAL is detected, before enrichment runs.

Renamed from `ContestEnrichmentCompleted` in the contest-finalization event restructure — see [docs/contest-finalization-event-restructure.md](../../contest-finalization-event-restructure.md).

## Flow Diagram

```mermaid
sequenceDiagram
    participant SR as Producer.CompetitionStreamerBase
    participant CH as Producer.ContestCompletedHandler
    participant EP as Producer.{Sport}ContestEnrichmentProcessor
    participant B as Message Bus
    participant FH as API.ContestFinalizedHandler

    SR->>B: Publish ContestCompleted (STATUS_FINAL detected)
    B-->>CH: Consume ContestCompleted
    CH->>CH: Schedule EnrichContestCommand (+30s)
    Note over CH, EP: Delay gives canonical status re-source time to land
    CH->>EP: Process(EnrichContestCommand)
    EP->>EP: Write Contest.AwayScore/HomeScore/WinnerFranchiseId/FinalizedUtc
    EP->>EP: Enrich CompetitionOdds
    EP->>B: Publish ContestFinalized
    B-->>FH: Consume ContestFinalized
    FH->>FH: Enqueue ScoreContestCommand
```
