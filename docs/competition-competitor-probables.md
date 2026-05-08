# MLB CompetitionCompetitor — Probables ingestion

Captured 2026-05-08. Phase 2 of the
[CompetitionCompetitor sport-specific split](competition-competitor-split.md).
Phase 1 created `BaseballCompetitionCompetitor` as a TPH subtype of the
shared `CompetitionCompetitorBase`. This phase hangs MLB's `probables[]`
collection off that subtype and wires inline ingestion into the
EventCompetitionCompetitor processor.

## What ESPN ships

The MLB EventCompetitionCompetitor payload carries an inline `probables`
array. Today it contains a single entry — the probable starting pitcher
— but the array shape leaves room for future roles (closer, etc.).

```json
"probables": [
  {
    "name": "probableStartingPitcher",
    "displayName": "Probable Starting Pitcher",
    "shortDisplayName": "Starter",
    "abbreviation": "SP",
    "playerId": 4311625,
    "athlete": { "$ref": ".../seasons/2026/athletes/4311625?lang=en&region=us" },
    "statistics": { "$ref": ".../seasons/2026/types/2/athletes/4311625/statistics/0?..." }
  }
]
```

Football has no analogue, so this lives entirely on the baseball side
of the split.

## Schema

New canonical entity: `CompetitionCompetitorProbable` — a 1:N child of
`BaseballCompetitionCompetitor`.

| Column | Notes |
|---|---|
| `Id` (PK) | Deterministic Guid: `Combine("competitor-probable", competitorId, name)` |
| `CompetitionCompetitorId` (FK, cascade) | Parent competitor row |
| `AthleteSeasonId` (FK, restrict) | Resolved from `athlete.$ref` |
| `EspnPlayerId` | Convenience copy of ESPN's int playerId |
| `Name` | Role key (`probableStartingPitcher`) |
| `DisplayName` / `ShortDisplayName` / `Abbreviation` | UI copy |

Unique index on `(CompetitionCompetitorId, Name)` so the role-name is
the natural key — reprocessing the same competitor with the same role
upserts the same row.

Cascade vs restrict choice:
- **Cascade from competitor** — a probable row is meaningless without
  its parent competitor; deleting the competitor takes its probables
  with it.
- **Restrict from athlete** — deleting an `AthleteSeason` should NOT
  cascade-cull historical probable rows. The probable is a record of
  what was advertised at game time; we want it to outlive the athlete's
  season row if that ever gets pruned.

## Ingestion path

`BaseballEventCompetitionCompetitorDocumentProcessor` overrides two
virtual hooks added to the base in this PR:

1. **`DeserializeDto`** — returns the sport-specific
   `EspnBaseballEventCompetitionCompetitorDto` so the base pipeline can
   pass the full payload (including `Probables`) through to the
   sport-specific hook.
2. **`ProcessSportSpecificCompetitorData`** — runs after the competitor
   entity is staged on the change tracker but before
   `SaveChangesAsync`, so probable rows commit in the same transaction
   as the competitor.

Per probable:
- Skip with a warning if `Name` or `Athlete.Ref` is missing.
- Resolve `AthleteSeasonId` via `IGenerateExternalRefIdentities`.
- If the AthleteSeason isn't in the DB yet:
  - `PublishDependencyRequest(... DocumentType.AthleteSeason)`
  - throw `ExternalDocumentNotSourcedException` so Hangfire retries
    the competitor document. This is the established not-sourced
    pattern; persisting a probable without a resolved athlete is
    worthless on the matchup card, so we fail loud and retry.
- Upsert by deterministic Id; the `(competitorId, role-name)` key
  collapses repeat runs onto the same row and updates the audit fields.

## Why not...

- **...persist the probable with a null `AthleteSeasonId`?** An empty
  probable is worthless on the matchup card. The not-sourced retry
  pattern is the established convention here.
- **...denormalize the pitcher's statistics onto the competitor?** ESPN
  ships a `statistics.$ref` per probable but those numbers drift through
  the season. Phase 3 will snapshot a small set of stats on the
  probable row at game-start time (ERA, W-L, K, etc.) so the matchup
  card renders the at-game-start view rather than today's rolled-up
  numbers — same time-travel problem we already solved for series
  state in PR #299. Out of scope here.
- **...add a `Role` enum?** Today only `probableStartingPitcher`
  exists. A string column with a unique-index discriminator keeps the
  schema flexible if ESPN starts shipping additional roles. We can
  promote to an enum once a second role appears and the closed set
  stops being speculative.

## Out of scope (later phases)

- Phase 3: pitcher stat snapshot (the time-travel concern above).
- Phase 4: UI surface — matchup card / Command Center tile rendering
  the probable starter pair.
