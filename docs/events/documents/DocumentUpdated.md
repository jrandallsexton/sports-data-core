# DocumentUpdated

Fired when an existing document is updated.

## Flow Diagram

```mermaid
sequenceDiagram
    participant P as Producer
    participant B as Message Bus
    participant A as API

    Note over P, A: Document Update
    P->>B: Publish DocumentUpdated
    B-->>A: (No Consumer Configured)
```
