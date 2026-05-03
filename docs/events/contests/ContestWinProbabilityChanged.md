# ContestWinProbabilityChanged

Win-probability tick. Producer publishes when ESPN's per-contest
probability snapshot moves. Sport-neutral — both football and baseball
carry the same home/away/tie percentages.

## Flow Diagram

```mermaid
sequenceDiagram
    participant P as Producer
    participant B as Message Bus
    participant A as API

    Note over P, A: Win probability moved
    P->>B: Publish ContestWinProbabilityChanged
    B-->>A: (No consumer configured today)
```

## Payload

| Field | Type |
|---|---|
| `ContestId` | Guid |
| `PlayId` | Guid? |
| `HomeWinPercentage` | double |
| `AwayWinPercentage` | double |
| `TiePercentage` | double |
| `SecondsLeft` | int |
| `EspnLastModifiedUtc` | DateTime |
| `Source` | string |
| `SourceRef` | string |
| `SequenceNumber` | string |
| `Ref` | Uri? |
| `Sport` | enum |
| `SeasonYear` | int? |
| `CorrelationId` | Guid |
| `CausationId` | Guid |
| `MessageId` | Guid (inherited from `EventBase` — auto `Guid.NewGuid()`) |
| `CreatedUtc` | DateTime (inherited from `EventBase` — UTC timestamp at construction) |
