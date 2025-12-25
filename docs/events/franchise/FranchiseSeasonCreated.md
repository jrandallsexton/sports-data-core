# FranchiseSeasonCreated

Fired when a franchise is associated with a season.

## Flow Diagram

```mermaid
sequenceDiagram
    participant P as Producer
    participant B as Message Bus
    participant A as API

    Note over P, A: Franchise Season Creation
    P->>B: Publish FranchiseSeasonCreated
    B-->>A: (No Consumer Configured)
```
