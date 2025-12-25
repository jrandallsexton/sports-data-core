# VenueUpdated

Fired when an existing venue's details are updated.

## Flow Diagram

```mermaid
sequenceDiagram
    participant P as Producer
    participant B as Message Bus
    participant A as API

    Note over P, A: Venue Update
    P->>B: Publish VenueUpdated
    B-->>A: (No Consumer Configured)
```
