# ConferenceUpdated

Fired when static conference details change.

## Flow Diagram

```mermaid
sequenceDiagram
    participant P as Producer
    participant B as Message Bus
    participant A as API

    Note over P, A: Conference Update
    P->>B: Publish ConferenceUpdated
    B-->>A: (No Consumer Configured)
```
