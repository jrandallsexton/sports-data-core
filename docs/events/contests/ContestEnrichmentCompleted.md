# ContestEnrichmentCompleted

Fired when a contest has been fully enriched with additional data.

## Flow Diagram

```mermaid
sequenceDiagram
    participant P as Producer
    participant B as Message Bus
    participant A as API

    Note over P, A: Contest Enrichment
    P->>B: Publish ContestEnrichmentCompleted
    B-->>A: (No Consumer Configured)
```
