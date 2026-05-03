# ContestStatusChanged

Sport-neutral contest lifecycle event. Producer publishes when a contest
transitions Scheduled → InProgress → Final (or related lifecycle states
such as Postponed). Per-play scoreboard ticks are split off to
`FootballContestStateChanged` / `BaseballContestStateChanged`.

## Flow Diagram

```mermaid
sequenceDiagram
    participant P as Producer
    participant B as Message Bus
    participant A as API
    participant S as SignalR Hub
    participant C as Web/Mobile clients

    Note over P, A: Lifecycle transition
    P->>B: Publish ContestStatusChanged
    B->>A: Consume ContestStatusChanged
    A->>S: SendAsync("ContestStatusChanged", payload)
    S->>C: Broadcast to all clients
```

## Payload (sport-neutral)

| Field | Type | Notes |
|---|---|---|
| `ContestId` | Guid | Contest aggregate root id |
| `Status` | string | `Scheduled` / `InProgress` / `Final` / etc. |
| `Ref` | Uri? | Producer-canonical resource ref |
| `Sport` | enum | |
| `SeasonYear` | int? | |
| `CorrelationId` | Guid | |
| `CausationId` | Guid | |
