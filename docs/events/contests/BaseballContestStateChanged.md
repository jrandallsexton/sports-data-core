# BaseballContestStateChanged

Baseball per-pitch / per-at-bat scoreboard tick. The API consumer and
SignalR fan-out (`BaseballContestStateChanged`) are wired so the
end-to-end pipeline lights up the moment the MLB live-state Producer
emitter ships. No Producer emitter today.

## Flow Diagram

```mermaid
sequenceDiagram
    participant P as Producer
    participant B as Message Bus
    participant A as API
    participant S as SignalR Hub
    participant W as Web/Mobile clients

    Note over P, A: Live MLB tick (planned)
    P->>B: Publish BaseballContestStateChanged
    B->>A: Consume BaseballContestStateChanged
    A->>S: SendAsync("BaseballContestStateChanged", payload)
    S->>W: Live MLB scoreboard tick
```

## Payload

| Field | Type | Notes |
|---|---|---|
| `ContestId` | Guid | |
| `Inning` | int | |
| `HalfInning` | string | `Top` / `Bottom` |
| `AwayScore` | int | |
| `HomeScore` | int | |
| `Balls` | int | |
| `Strikes` | int | |
| `Outs` | int | |
| `RunnerOnFirst` | bool | |
| `RunnerOnSecond` | bool | |
| `RunnerOnThird` | bool | |
| `AtBatAthleteId` | Guid? | |
| `PitchingAthleteId` | Guid? | |
| `Ref` | Uri? | |
| `Sport` | enum | `BaseballMlb` |
| `SeasonYear` | int? | |
| `CorrelationId` | Guid | |
| `CausationId` | Guid | |
| `MessageId` | Guid | Inherited from `EventBase` — auto-generated `Guid.NewGuid()`. |
| `CreatedUtc` | DateTime | Inherited from `EventBase` — UTC timestamp at construction. |
