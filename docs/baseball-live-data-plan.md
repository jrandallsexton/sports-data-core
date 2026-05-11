# Baseball Live Data — capture & emission plan

Working notes for capturing the rich per-play data ESPN gives us for
MLB and surfacing it through the live event surface so the matchup
card (and eventually the contest overview) can show real game state
instead of placeholder defaults. Use as context in follow-up sessions;
update in place as decisions land.

> **Why this matters now.** Live MLB UI is currently rendering
> defaults — empty `HalfInning`, `0` outs, all-false runners,
> `null` athlete IDs — because the play-emission pipeline emits
> placeholder values regardless of what ESPN sent us. Until that
> pipeline carries real data, the rows added in PR #309
> (`BaseballGameStatusInProgress`) stay mostly suppressed.
> MLB launch target is **Spring 2027** with college baseball as
> a likely follow-on; this is the foundation work that lights up
> every downstream live experience.

## Current state — what's actually wired

### Entity (`BaseballCompetitionPlay`)

The entity definition is generous and already has columns for most
ESPN fields:

```text
✅ Existing columns:
  AtBatId, AtBatPitchNumber, BatOrder, BatsType, BatsAbbreviation,
  PitchCoordinateX/Y, HitCoordinateX/Y, PitchTypeId/Text/Abbreviation,
  PitchVelocity, PitchCountBalls/Strikes, ResultCountBalls/Strikes,
  Trajectory, StrikeType, SummaryType,
  AwayHits, HomeHits, AwayErrors, HomeErrors, RbiCount,
  IsDoublePlay, IsTriplePlay
```

The entity inherits from `CompetitionPlayBase`, which contributes the
shared columns (`Id`, `EspnId`, `SequenceNumber`, `Type`, `TypeId`,
`Text`, `AlternativeText`, `AwayScore`, `HomeScore`, `PeriodNumber`,
`ScoringPlay`, `Priority`, `ScoreValue`, `Modified`, `StartFranchiseSeasonId`).

### DTO (`EspnBaseballEventCompetitionPlayDto`)

The DTO declares the baseball-specific shape that ESPN actually sends:

```text
✅ DTO fields today:
  Valid, AtBatId, SummaryType,
  PitchCount (Balls/Strikes), ResultCount (Balls/Strikes),
  Outs, RbiCount,
  AwayHits, HomeHits, AwayErrors, HomeErrors,
  DoublePlay, TriplePlay
```

Plus the inherited shared fields from `EspnEventCompetitionPlayDtoBase`.

### Extraction (`CompetitionPlayExtensions.AsBaseballEntity`)

**This is the core gap.** The extension method maps almost nothing
sport-specific:

```csharp
var entity = new BaseballCompetitionPlay
{
    Id = identity.CanonicalId,
    EspnId = dto.Id,
    SequenceNumber = dto.SequenceNumber,
    Text = dto.Text ?? "UNK",
    TypeId = dto.Type?.Id ?? "9999"
};

MapSharedProperties(...)   // shared fields only
return entity;
```

**Every baseball-specific column on the entity stays null.** The DTO
has the data, the entity has the columns, but the wiring between them
doesn't exist for baseball. Football's `AsFootballEntity` does
populate its sport-specific shape; baseball's was stubbed out and
never filled in.

### Event payload (`BaseballPlayCompleted`)

`BaseballEventCompetitionPlayDocumentProcessor.PublishSportPlayCompletedAsync`
emits hardcoded defaults for everything beyond what's on the
entity-level base + ResultCount:

```csharp
HalfInning: string.Empty,        // not on entity, not on DTO
Outs: 0,                          // not on entity (DTO has it)
RunnerOnFirst/Second/Third: false,  // not anywhere
AtBatAthleteId: null,             // not on entity, not on DTO (participants needed)
PitchingAthleteId: null,          // same
```

## Field shape on the wire (sample analysis)

The first 25 plays from `EventCompetitionPlays.json` (game
401815268, page 1 of 26) show:

| ESPN play field | Sample value | Captured today? |
|---|---|---|
| `id`, `sequenceNumber` | "4018152680000000059", "1" | ✅ on entity, mapped |
| `type.{id,text,type}` | `{59, "Start Inning", "start-inning"}` | ✅ id mapped (TypeId), text not |
| `text`, `shortText`, `alternativeText` | "Top of the 1st inning" | ✅ shared |
| `period.number` | `1` | ✅ as PeriodNumber |
| `period.type` | `"Top"` / `"Bottom"` | ❌ **DTO missing this field** |
| `period.displayValue` | `"1st Inning"` | ❌ DTO missing |
| `awayScore` / `homeScore` | `0` / `0` | ✅ shared |
| `team.$ref` | `…/teams/3` | ✅ resolved → StartFranchiseSeasonId |
| `wallclock` | "2026-05-09T19:08:04Z" | ❌ not captured (Modified is server-side) |
| `atBatId` | `"4018152680001"` | ✅ DTO has it, ❌ extension doesn't map it |
| `summaryType` | `"I"` / `"A"` / `"P"` | ✅ DTO has it, ❌ extension doesn't map |
| `pitchCount.{balls,strikes}` | `{0,0}` | ✅ DTO has it, ❌ extension doesn't map |
| `resultCount.{balls,strikes}` | `{0,1}` | ✅ DTO has it, ❌ extension doesn't map |
| `outs` | `0` / `1` | ✅ DTO has it, ❌ no entity column, ❌ event hardcodes 0 |
| `rbiCount`, `awayHits`, `homeHits`, `awayErrors`, `homeErrors` | various | ✅ DTO, ❌ extension |
| `doublePlay`, `triplePlay` | `false` | ✅ DTO, ❌ extension |
| `valid` | `true` | ✅ DTO has it, ❌ entity doesn't |
| `participants[]` | `[{athlete, position, type:"pitcher"}, {…, type:"batter"}]` | ❌ **DTO missing entirely**; entity has no columns |
| `batOrder` | `1` | ❌ DTO missing; entity has BatOrder column |
| `bats.{type,abbreviation,displayValue}` | `{"RIGHT","R","Right"}` | ❌ DTO missing; entity has BatsType / BatsAbbreviation |
| `pitches.{type,abbreviation,displayValue}` | `{"RIGHT","R","Right"}` | ❌ DTO missing entirely (pitcher handedness) |
| `atBatPitchNumber` | `1`, `2`, `3` | ❌ DTO missing; entity has AtBatPitchNumber |
| `pitchCoordinate.{x,y}` | `{118, 162}` (strike-zone ish) | ❌ DTO missing; entity has PitchCoordinateX/Y |
| `pitchType.{id,text,abbreviation}` | `{17, "Four-seam FB", "FF"}` | ❌ DTO missing; entity has PitchTypeId/Text/Abbreviation |
| `pitchVelocity` | `95` (mph) | ❌ DTO missing; entity has PitchVelocity |
| `strikeType` | `"swinging"` / `"foul"` / `"looking"` | ❌ DTO missing; entity has StrikeType |
| `previousPlayText` / `shortPreviousPlayText` | flavor text on `play-result` | ❌ DTO missing |
| `alternativeType` / `alternativePlay` | available on `play-result` | ❌ DTO missing |
| `probability` | `play-result` win-prob delta | ❌ DTO missing |

### Notably absent from the play payload itself

- **Runner state on bases.** No `runners[]`, `onFirst`, etc. anywhere
  in any play (sampled across all 25 in the page). Comes from
  `EventCompetitionSituation` (separate ESPN doc, processed in
  football today via `EventCompetitionSituationDocumentProcessor`).
- **Hit coordinates.** Only appear on `play-result` types when
  there's contact; none in the sampled 25 plays.

## Play-type taxonomy (from sample)

Sampled types that appear on a single inning's worth of plays:

```text
start-inning              opens a half-inning ("Top of the 1st inning")
start-batterpitcher       opens an at-bat ("X pitches to Y")
ball                      ball
strike-swinging           swinging strike
strike-looking            called strike
foul-ball                 foul ball
play-result               at-bat outcome (hit, walk, strikeout, …)
end-batterpitcher         closes an at-bat
```

Other types we'll see in the wild but not in the page-1 sample:
`foul-tip`, `hit-by-pitch`, `pitcher-substitution`,
`offensive-substitution`, `defensive-substitution`, `pickoff`,
`stolen-base`, `caught-stealing`, `wild-pitch`, `passed-ball`, etc.
The full taxonomy lives behind ESPN's play-type dictionary; we don't
need to enumerate it for capture, just store `TypeId` and the text.

## Phasing

### Phase 1 — DTO + extraction symmetry **(this PR)**

**Status:** in flight on `feat/mlb-canonical-play-data`.
**Scope locked at:** full ingestion of the rich per-play data. No
event payload changes, no UI changes, no replay changes — those are
Phase 2. Capture-only means: every field ESPN gives us at the play
level lands on the entity. Athlete resolution from `participants[]`
*is* in scope here because it is part of capture (the play row is
incomplete without it).

**Scope:** make `EspnBaseballEventCompetitionPlayDto` carry every
field the JSON sample shows, then make `AsBaseballEntity` populate
every column the entity already has (plus the new ones), including
resolved canonical Athlete IDs from `participants[]`.

**DTO additions:**
- `EspnEventCompetitionPlayPeriodDto.Type` (string) — adds Top/Bottom.
- `EspnEventCompetitionPlayPeriodDto.DisplayValue` (string).
- `EspnBaseballEventCompetitionPlayDto`:
  - `BatOrder`, `Bats` (reuses existing `EspnAthleteHandDto`),
    `Pitches` (same DTO type), `AtBatPitchNumber`, `PitchCoordinate`,
    `PitchType`, `PitchVelocity`, `StrikeType`, `Participants[]`
    (reuses existing `EspnEventCompetitionPlayParticipantDto`),
    `PreviousPlayText`, `ShortPreviousPlayText`, `AlternativeType`,
    `Probability`, `HitCoordinate`, `Trajectory`.
- New small DTOs: `EspnBaseballCoordinateDto` (x/y),
  `EspnBaseballPitchTypeDto` (id/text/abbreviation).

**Entity additions on `BaseballCompetitionPlay`:**
- `HalfInning` (string?, max 8) — captures `period.type`.
- `Outs` (int?) — captures play-level outs.
- `Wallclock` (DateTime?) — actual play time, distinct from
  server-side `Modified`.
- `IsValid` (bool, default true) — ESPN's `valid` flag.
- `PitchesType` (string?, max 20) and `PitchesAbbreviation`
  (string?, max 5) — pitcher handedness, mirrors existing
  `BatsType` / `BatsAbbreviation`.
- `AtBatAthleteSeasonId` (Guid?) and `PitchingAthleteSeasonId`
  (Guid?) — denormalized convenience copy of the primary
  pitcher/batter AthleteSeason IDs, sourced from the participants
  table below. The athlete refs on play participants are
  season-scoped (`/seasons/{year}/athletes/{id}`), so they resolve
  to `AthleteSeason`, not the global `AthleteBase`. Cheap live-UI
  lookup without an extra join. ID-only references (no FK
  navigation), matching the `*FranchiseSeasonId` pattern on plays.

**New entity hierarchy `CompetitionPlayParticipantBase` →
`BaseballCompetitionPlayParticipant`:**
Mirrors the existing `CompetitionPlayBase` /
`BaseballCompetitionPlay` / `FootballCompetitionPlay` TPH split —
one shared sport-agnostic table (`CompetitionPlayParticipant`)
with EF auto-generating the `Discriminator` column. The abstract
base lives in `Producer/Infrastructure/Data/Entities/`; the
sport-specific subclass lives under `Baseball/Entities/` and owns
the FK navigation back to `BaseballCompetitionPlay`. A future
Football PR adds its own `FootballCompetitionPlayParticipant`
into the same table.

The abstract base is registered only in `BaseballDataContext`
(not `TeamSportDataContext`) so a sport without a participant
subclass doesn't drag the abstract base into its model — EF would
fail validation otherwise (no concrete derived type).

Source of truth for participant data; the denormalized columns
above are convenience copies. Schema:

- `Id` (Guid, PK), audit columns from `CanonicalEntityBase`.
- `CompetitionPlayId` (Guid, FK → CompetitionPlay, cascade delete).
- `Order` (int) — ESPN's order within the array.
- `Type` (string, max 32) — "pitcher", "batter", and any future
  taxonomy entries. Persisted verbatim; unknown types log a Seq
  warning but are not dropped.
- `AthleteSeasonId` (Guid?) — resolved canonical AthleteSeason
  (the play's `participant.athlete.$ref` is season-scoped, so it
  points at the season-bound athlete row, not the global
  AthleteBase); null when not yet sourced. Update path
  re-resolves on each re-ingest.
- `PositionId` (Guid?) — resolved canonical AthletePosition;
  same null-on-unsourced semantics.
- `AthleteRef` / `PositionRef` / `StatisticsRef` (string?, max 512) —
  ESPN refs preserved verbatim for audit + later re-resolution.
  Position has no canonical entity yet; per-play statistics are a
  future processor target.
- Indexes: `(CompetitionPlayId, Type)` for "pitcher for play X"
  lookups, `(AthleteSeasonId)` for "all plays involving
  athlete-season Y", `(PositionId)` for position-based queries.

**Extension + processor changes:**
- `AsBaseballEntity` now takes
  `(atBatAthleteSeasonId, pitchingAthleteSeasonId)` alongside
  `teamFranchiseSeasonId`, and populates every baseball column from
  the DTO.
- `BaseballEventCompetitionPlayDocumentProcessor` has two helpers:
  - `BuildParticipantsAsync` walks `participants[]` and emits a
    `List<BaseballCompetitionPlayParticipant>`, resolving each
    `athlete.$ref` via
    `ResolveIdAsync<AthleteSeason, AthleteSeasonExternalId>` (the ref
    is season-scoped, not global) and each `position.$ref` via
    `ResolveIdAsync<AthletePosition, AthletePositionExternalId>` where
    possible. Unrecognized `Type` logs a Seq warning but persists the
    row anyway — we don't drop data.
  - `ExtractPrimaryAthletes` pulls the first pitcher/batter out of
    that list for the denormalized columns.
- `BuildNewPlayAsync` builds the participants, attaches them to
  `play.Participants`, and EF cascades the insert.
- `ApplyUpdateAsync` reloads the participants set, removes them,
  and re-adds the new build — simpler than diffing per-row, and
  ESPN can re-order or swap participants on a play between
  fetches (e.g., a substitution row appearing on a later refresh).
- Empty/null participants (e.g. `start-inning`,
  `end-batterpitcher`) yield empty lists. Unresolved athlete-seasons
  (race during first ingest) yield null `AthleteSeasonId` — the
  update path re-resolves on each re-ingest, so transient nulls
  heal naturally without a throw-retry storm.

**Migration:** `AddBaseballPlayCanonicalCaptureFields`. Eight
nullable columns on `CompetitionPlay` + `IsValid` non-nullable with
`defaultValue: true` so existing rows on the shared TPH table
don't break, plus the new `BaseballCompetitionPlayParticipant`
table with FK + two indexes.

**Risk:** medium. Migration on a hot table; new DTO fields are
additive (deserializer ignores unknowns by default). Athlete
resolution adds one DB round-trip per participant — acceptable at
MLB cadence (one play every ~15s during action), and most plays
have exactly two participants.

**Football is intentionally out of scope.** Football's
`EspnFootballEventCompetitionPlayDto` also carries `Participants`
and `TeamParticipants` arrays that are not currently captured.
That's its own PR — the shapes overlap on `Participants` but
diverge on `TeamParticipants`, and Football has additional
`teamParticipants[]` work that doesn't exist in baseball. Avoid
folding it in here to keep the migration scope sport-bounded.

### Phase 2 — Event population + replay drop the defaults

**Scope:** wire the captured fields onto `BaseballPlayCompleted` and
the replay service so the matchup card stops showing placeholders.

**Entity additions:** none — Phase 1 captures everything.

**Event population (`PublishSportPlayCompletedAsync`):**
- `HalfInning: baseballPlay.HalfInning ?? string.Empty`
- `Outs: baseballPlay.Outs ?? 0`
- `AtBatAthleteSeasonId: baseballPlay.AtBatAthleteSeasonId`
- `PitchingAthleteSeasonId: baseballPlay.PitchingAthleteSeasonId`
- (Balls/Strikes already wired off `ResultCount*`.)

The existing `BaseballPlayCompleted` event fields are named
`AtBatAthleteId` / `PitchingAthleteId`; Phase 2 also renames them
to `*AthleteSeasonId` so the wire shape matches the entity columns
and the season-scoped resolution is explicit on the consumer side.

**Replay service mirror.** `BaseballContestReplayService` reads
from the entity, so once Phase 2 lands it picks up the new columns
automatically — just remove the hardcoded defaults.

**UI consequence (free):** `BaseballGameStatusInProgress` already
renders these fields when present. The ⚾ batting indicator (driven
by `halfInning === 'top'/'bottom'`) starts working. The
inning+count+outs row stops showing `0-0 · 0 outs` placeholder
values for real plays.

**Risk:** low. Pure emission shift; rollback is a one-line revert
of the publish payload.

### Phase 3 — Runner state from situation document

**Scope:** populate `runnerOnFirst/Second/Third` on `BaseballPlayCompleted`.

**Why this is its own phase:** runner state isn't on the play
payload. It comes from `EventCompetitionSituation` (which football
already sources via `EventCompetitionSituationDocumentProcessor`).
For MLB it carries the current base state plus current at-bat /
pitcher athlete IDs.

**Design choice (open):**

- **Option A — emit a `BaseballSituationChanged` event.** Producer
  has a separate processor for the situation doc that emits its own
  event; UI merges into the same context record. Keeps responsibility
  separated. Cost: one more event type; UI handler addition; possible
  coordination problem (situation arrives before the play it
  "belongs to").
- **Option B — join situation onto play emission.** When the play
  processor is about to publish, it queries the latest
  `BaseballCompetitionSituation` row for that competition and folds
  the runners + at-bat IDs into the `BaseballPlayCompleted` payload.
  Cost: extra DB read on every play; coupling; risk of stale
  situation if the situation doc lags the play doc.
- **Option C — hold the play, wait for situation.** Buffer or
  conditionally retry the play emission until the matching
  situation row is present. Most accurate; most complex.

Recommendation: **Option A** for symmetry with football's situation
processor, deferred to its own PR after Phase 1+2 are in. It also
gives the UI a useful event independently (situation changes that
don't happen on a play boundary, e.g., a runner caught stealing
between pitches, become first-class).

### Phase 4 — UI enrichment (optional, deferrable)

Once the plumbing is real, the matchup card can show more without
shipping new wire fields:

- Pitch ticker on the matchup card or contest overview:
  `pitchType.text` + `pitchVelocity` per pitch, last N pitches.
- At-bat header: `${pitcher} pitches to ${batter}` derived from
  resolved athlete IDs (resolve names via existing athlete lookup).
- Pitch handedness flavor: "RHP vs LHB" badge.

These are low-risk UI additions that don't touch the wire shape.

### Phase 5 — Hit coordinates / spray chart (later)

`play-result` plays carry `hitCoordinate.{x,y}` plus `trajectory`.
Useful for a spray chart on the contest overview, NOT on the
matchup card. Defer until contest overview MLB work is on the
roadmap.

## Open questions

1. ~~**Phase boundaries.**~~ **Resolved 2026-05-10:** Phase 1 covers
   *all* play-level capture (including athlete resolution from
   `participants[]`). Phase 2 is reduced to "drop the defaults on
   the event payload + replay" — no entity work left to do there.

2. ~~**Athlete unresolved race.**~~ **Resolved 2026-05-10:** persist
   null when the athlete ref hasn't been sourced yet. The play
   update path re-resolves on each re-ingest (which happens
   regularly for in-progress games), so transient nulls heal
   without a throw-retry storm on every play of a freshly-sourced
   game. The capture-only philosophy: persist what we can, don't
   fail the document.

3. **`Valid` flag.** ESPN's `valid: true/false` on a play — is `false`
   ever real? If so it's a hint to suppress. Punt to discovery
   during local replay testing; not a blocker.

4. **Wallclock vs Modified.** The DTO base captures `Modified`
   (server-side). `wallclock` is the actual play time. We probably
   want both eventually for play timelines; not blocking live UI.

## Reference

- ESPN play sample (page 1 of 26 = first 25 plays):
  `test/unit/SportsData.Producer.Tests.Unit/Data/EspnBaseballMlb/EventCompetitionPlays.json`
- Existing entity:
  `src/SportsData.Producer/Infrastructure/Data/Baseball/Entities/BaseballCompetitionPlay.cs`
- Existing DTO:
  `src/SportsData.Core/Infrastructure/DataSources/Espn/Dtos/Baseball/EspnBaseballEventCompetitionPlayDto.cs`
- Existing extraction:
  `src/SportsData.Producer/Infrastructure/Data/Entities/Extensions/CompetitionPlayExtensions.cs`
- Existing processor:
  `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Baseball/BaseballEventCompetitionPlayDocumentProcessor.cs`
- Existing event:
  `src/SportsData.Core/Eventing/Events/Contests/Baseball/BaseballPlayCompleted.cs`
- Existing replay:
  `src/SportsData.Producer/Application/Contests/BaseballContestReplayService.cs`
