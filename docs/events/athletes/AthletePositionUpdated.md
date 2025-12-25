# AthletePositionUpdated

Fired when an athlete's position details are updated.

## Flow Diagram

```mermaid
sequenceDiagram
    participant P as Producer
    participant B as Message Bus
    participant A as API

    Note over P, A: Athlete Position Update
    P->>B: Publish AthletePositionUpdated
    B-->>A: (No Consumer Configured)
```
