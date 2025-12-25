# CompetitionWinProbabilityChanged

Fired when the win probability for a competition changes.

## Flow Diagram

```mermaid
sequenceDiagram
    participant P as Producer
    participant B as Message Bus
    participant A as API

    Note over P, A: Win Probability Update
    P->>B: Publish CompetitionWinProbabilityChanged
    B-->>A: (No Consumer Configured)
```
