# FranchiseCreated

Fired when a new franchise (team) is created.

## Flow Diagram

```mermaid
sequenceDiagram
    participant P as Producer
    participant B as Message Bus
    participant A as API

    Note over P, A: Franchise Creation
    P->>B: Publish FranchiseCreated
    B-->>A: (No Consumer Configured)
```
