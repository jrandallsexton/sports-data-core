# FranchiseUpdated

Fired when franchise details are updated.

## Flow Diagram

```mermaid
sequenceDiagram
    participant P as Producer
    participant B as Message Bus
    participant A as API

    Note over P, A: Franchise Update
    P->>B: Publish FranchiseUpdated
    B-->>A: (No Consumer Configured)
```
