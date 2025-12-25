# ConferenceSeasonUpdated

Fired when conference season details change.

## Flow Diagram

```mermaid
sequenceDiagram
    participant P as Producer
    participant B as Message Bus
    participant A as API

    Note over P, A: Conference Season Update
    P->>B: Publish ConferenceSeasonUpdated
    B-->>A: (No Consumer Configured)
```
