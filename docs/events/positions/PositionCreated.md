# PositionCreated

Fired when a new position definition is created or imported.

## Flow Diagram

```mermaid
sequenceDiagram
    participant P as Producer
    participant B as Message Bus
    participant A as API

    Note over P, A: Position Creation
    P->>B: Publish PositionCreated
    B-->>A: (No Consumer Configured)
```
