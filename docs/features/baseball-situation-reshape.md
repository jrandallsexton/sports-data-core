# Baseball Situation Reshape (audit #2 — baserunners)

Status: **Proposed / awaiting authorization**
Last updated: 2026-07-22

## Problem

`BaseballEventCompetitionSituationDocumentProcessor` writes the shared,
football-shaped `CompetitionSituation` entity with `Down/Distance/YardLine = 0`
and **drops all baseball situation data**: `balls`, `strikes`, `outs`,
`onFirst`/`onSecond`/`onThird` (baserunner occupancy), and `situationNotes`.
This is why `BaseballEventCompetitionPlayDocumentProcessor` publishes hardcoded
`RunnerOnFirst/Second/Third: false` — the live diamond can never show men on
base. The processor's own comment says this "will be addressed with
sport-specific situation entities in a future refactor." This is that refactor.

## Current shape

- `CompetitionSituation` (single concrete entity) — football fields
  (`Down`, `Distance`, `YardLine`, `IsRedZone`, `AwayTimeouts`, `HomeTimeouts`)
  + check constraints. Registered in the **shared** `TeamSportDataContext`, so
  **both** sport DBs carry the football-shaped table.
- `CompetitionBase.Situations` and `CompetitionPlayBase.SituationsAsLastPlay`
  navigations reference the concrete type.
- Readers (`BaseballCompetitionStreamer`, `FootballCompetitionStreamer`,
  `BaseballContestReplayService`, `ContestUpdateProcessor`) reference the type
  but **do not** touch football-specific fields — low read-side risk.

## Proposed design — TPH split (mirrors the CompetitionPlay split)

Matches the established `CompetitionPlayBase` / `FootballCompetitionPlay` /
`BaseballCompetitionPlay` pattern and the "sport-specific subtype over nullable"
convention (don't pile baseball nullables on the football-shaped parent).

1. **`CompetitionSituationBase`** (abstract): `Id`, `CompetitionId` (+ nav),
   `LastPlayId` (+ nav). Shared FK/index config on the base; table
   `CompetitionSituation`, EF auto-discriminator.
2. **`FootballCompetitionSituation`**: the current football fields + check
   constraints (unchanged behavior). Registered in `FootballDataContext` only.
3. **`BaseballCompetitionSituation`**: `Balls`, `Strikes`, `Outs`,
   `OnFirstAthleteSeasonId`, `OnSecondAthleteSeasonId`, `OnThirdAthleteSeasonId`
   (nullable FK → `AthleteSeason`). Registered in `BaseballDataContext` only.
4. **`BaseballCompetitionSituationNote`** child table: `SituationId` FK, `Type`,
   `Text` — captures `situationNotes[]` verbatim (mirrors the participant table
   approach; "capture everything ESPN publishes").
5. Move situation config registration **out of** `TeamSportDataContext` into the
   two sport contexts (football registers base + football subtype; baseball
   registers base + baseball subtype + note), exactly like the play/participant
   split. Retype the two navigations to `CompetitionSituationBase`.

## Baserunner resolution — same pattern as #545

`onFirst/onSecond/onThird` carry a season-scoped `athlete` ref → resolve to
`AthleteSeason` via `ResolveIdAsync`. When a baserunner's athlete isn't sourced
yet, `PublishDependencyRequest(DocumentType.AthleteSeason)` and throw
`ExternalDocumentNotSourcedException` so the base retries the situation after it
lands. All missing baserunners are requested before a single throw (one retry
cycle sources them all). The processor already uses this exact pattern for
`LastPlay`, so it's consistent within the same method.

## Migrations

- **Football context**: add the `Discriminator` column to the existing
  `CompetitionSituation` table; football columns unchanged. Minimal, non-destructive.
- **Baseball context**: drop the football columns + their check constraints from
  the `CompetitionSituation` table, add the baseball columns + `Discriminator`,
  create `BaseballCompetitionSituationNote`. Existing baseball rows are
  football-shaped placeholders (`Down=0`, etc.) — no real data lost. A one-line
  cleanup can truncate stale placeholder situations if preferred.

## Explicitly OUT of scope

- **Wiring `RunnerOnFirst/Second/Third` into `BaseballPlayCompleted`.** That
  reads the latest situation at play-completion time and belongs to the live
  data/broadcast phase (the play processor comment defers it). This PR only
  **captures** the baserunner state; the live event keeps its current safe
  defaults until that phase.
- `pitcher` / `batter` on the situation — already captured on the play
  (`AtBatAthleteSeasonId` / `PitchingAthleteSeasonId`). Can add to the situation
  later if a read path needs it; not required for baserunners.
- Football `teamParticipants` and other unrelated drops.

## Test plan

- Real captured baseball situation fixture with baserunners + notes.
- Assert `BaseballCompetitionSituation` persists balls/strikes/outs, resolved
  `OnFirst/Second/ThirdAthleteSeasonId`, and the notes rows.
- Assert an unsourced baserunner withholds the situation and requests
  `AthleteSeason` (the #545 withhold-and-request behavior).
- Football situation processor test stays green (now writes
  `FootballCompetitionSituation`).

## Estimated surface

Entity hierarchy (4 files) + 2 context registrations + 2 situation processors +
`CompetitionSituationExtensions` + 2 nav retypes + migrations (both contexts) +
tests. ~15 files. No read-side field changes required.
