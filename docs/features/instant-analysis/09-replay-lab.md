# 09. Admin replay lab

**Feature:** [Instant Analysis](instant-analysis.md) · **Phase:** 1 (proof of concept) · **Status:** done 2026-10-10 (uncommitted)

**Depends on:** [08 Persist and audit](08-persist-and-audit.md)

**Feature doc:** [Ship dark and the lab](instant-analysis.md#ship-dark-and-the-lab) · [Learning what's weak](instant-analysis.md#learning-whats-weak)

## Goal

Review real games before anything is user-facing, and start collecting the data that tells us what's weak.

## Scope

- Run the pipeline over a completed game's plays with a new `AnalysisRunId` (lab rows never mix with live).
- Compute "as of" the replayed game: season sums restricted to competitions that started before it.
- Bypass the per-game flag (completed games may have no stream row).
- An admin page: per play, the generated facts, the dropped facts with reasons, and 0–4 ratings (SmackBot Lab pattern).

**Out of scope:**

- Live delivery (task 10).

## Deliverables

- The replay endpoint/command, admin-only.
- The admin page (web).
- Rating storage.

## Acceptance criteria

- [x] Replaying the example game shows the interception's facts as they would have been at that moment. (Game facts exactly; its season facts are dropped because no pre-kickoff season document survives, see Outcome.)
- [x] Two runs of the same game don't collide.
- [x] Ratings are stored per fact and run.

## Notes

- The existing contest replay services re-publish stored plays; the lab may reuse them or drive the job directly.

## Outcome (2026-10-10)

**Producer** (`Application/Competitions/InstantAnalysis/`):

- The per-play pipeline moved out of the live job into `PlayAnalyser` (`IPlayAnalyser.Analyse(playId, contestId,
  correlationId, analysisRunId, SeasonDocumentRule)`), returning what it generated, persisted and dropped. The live
  job gates on the per-game flag and calls it with no run id and `Newest`; the lab calls it with its run id and
  `BeforeKickoff`, bypassing the flag.
- `Lab/`: `RunPlayAnalysisLabCommandHandler` (synchronous, like the admin re-enrich command: creates a
  `PlayAnalysisRun`, analyses every play in sequence order, stores the run's dropped facts and counts),
  `GetPlayAnalysisRunsQueryHandler`, `GetPlayAnalysisRunQueryHandler` (every play in order with its facts, ratings
  and drops), `RatePlayAnalysisCommandHandler` (0-4, upsert), and `InstantAnalysisLabController`
  (`api/instant-analysis/lab/...`).
- New tables (migration `10OctV2_InstantAnalysisLab`, Football and Baseball): **`PlayAnalysisRun`** (Id = the run's
  `AnalysisRunId`; counts, `TemplateVersion`, `DroppedFacts` jsonb, `CompletedUtc`; indexed by `ContestId`) and
  **`PlayAnalysisRating`** (one per fact: unique `PlayAnalysisId`, cascade FK, check `Stars` 0-4). Dropped facts
  live on the run because live drops only reach the logs.
- **Fix:** the instant-analysis registrations (task 03's job, now also the analyser and lab handlers) moved into the
  football-only DI block. They depend on `FootballDataContext`, which other sports don't register, so a baseball
  Producer would have failed DI validation on boot.

**API:** `Application/InstantAnalysisLab/InstantAnalysisLabController.cs`, `api/{sport}/{league}/instant-analysis-lab/...`,
`[AdminApiToken]`, thin relays through the sport's Producer contest client (`IProvideContests`: four new methods;
DTOs in `Core/.../Contest/Queries/InstantAnalysisLabDtos.cs`).

**Web:** `AdminInstantAnalysisLabPage.jsx` at `/app/admin/instant-analysis-lab` (admin nav link): league and contest
id (remembered), "Run lab replay", the game's runs, and per play the facts (with provenance and 0-4 stars) and the
dropped facts with reasons; plays with neither are hidden by default. The star widget is a copy of SmackBot Lab's,
not a shared extraction. Test: `AdminInstantAnalysisLabPage.test.jsx`.

### Local end-to-end (example game)

- Two runs: 200 plays each, **38 facts and 45 drops each**, 1.3-2.0 s, no collision. Live analysis of the same game
  produced 55 facts; the difference is its 17 season facts.
- The interception (sequence 8): "T. Cooley's first interception of the game.", "LSU's first takeaway of the
  game.", "J. Strong's first interception thrown this game."; its season facts dropped: "No season document written
  before kickoff."
- Drops: 44 "No season document written before kickoff.", 1 freshness-guard drop (a document from 09-14, older than
  the team's previous game's final on 09-27).
- Rating: 2 then 4 stars on one fact leaves one row with 4; 7 stars is rejected (400).
- Not exercised end to end: the API relay and the web page against a running API (unit-tested only).
