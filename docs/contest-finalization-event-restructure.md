# Contest finalization event restructure

**Status:** Proposed
**Date:** 2026-05-31
**Driver:** Picks scored against pre-enrichment Contest state, producing incorrect results

## Problem

API's `ContestCompletedHandler` consumes Producer's `ContestCompleted` event and immediately enqueues `ScoreContestCommand`. But `ContestCompleted` is published the *moment* `STATUS_FINAL` is detected — well before the canonical `Contest` row has been enriched with final scores, winner, odds results, or `FinalizedUtc`. Scoring then runs against incomplete data: SU picks have no winner to compare against, ATS picks have no `AtsWinnerFranchiseSeasonId`, O/U picks have no `OverUnderResult`.

The work API actually needs sits in `Producer.{Baseball|Football}ContestEnrichmentProcessor.Process` — currently triggered only by the daily cron `ContestEnrichmentJob`, never by the live `ContestCompleted` signal.

## Current flow (with bug)

```text
Producer.CompetitionStreamerBase  (STATUS_FINAL detected; 3 publish sites)
  ├─ publish ContestCompleted                    (Direct delivery)
  └─ publish DocumentRequested(Event)            async re-source canonical Event

API.ContestCompletedHandler                      [BUG: runs immediately]
  └─ enqueue ScoreContestCommand

API.ContestScoringProcessor
  └─ HTTP GET Producer.GetMatchupResult          [reads pre-enrichment data]

Producer.ContestEnrichmentJob   [CRON]
  └─ enqueue EnrichContestCommand for c.FinalizedUtc == null

Producer.{Sport}ContestEnrichmentProcessor
  ├─ guard: bails if CompetitionStatus != STATUS_FINAL
  ├─ resolves final scores
  ├─ writes Contest.AwayScore/HomeScore/WinnerFranchiseId/FinalizedUtc
  ├─ enriches CompetitionOdds (winner / ATS / O/U results)
  └─ publishes ContestEnrichmentCompleted        [ZERO consumers today]
```

## Proposed flow

```text
Producer.CompetitionStreamerBase                 unchanged
  ├─ publish ContestCompleted
  └─ publish DocumentRequested(Event)

Producer.ContestCompletedHandler   [NEW]         moved from API, redirected
  └─ Schedule EnrichContestCommand +30s          (D2: status-propagation delay)

Producer.{Sport}ContestEnrichmentProcessor
  ├─ short-circuit if Contest.FinalizedUtc != null   (D4: idempotency)
  ├─ ...existing work...
  └─ publishes ContestFinalized                  renamed from ContestEnrichmentCompleted (D1)

API.ContestFinalizedHandler   [NEW]              replaces ContestCompletedHandler
  └─ enqueue ScoreContestCommand

Producer.ContestEnrichmentJob   [CRON, kept]     permanent backstop (D3)
  └─ catches contests where the event-driven path missed
```

## Decisions

### D1. Rename `ContestEnrichmentCompleted` → `ContestFinalized`

The event already exists and is already published by both enrichment processors with no consumers anywhere. "Finalized" is the business concept; "EnrichmentCompleted" leaks an implementation detail. Zero-cost rename since there are no existing consumers to migrate.

Payload stays identical (`ContestId, Ref, Sport, SeasonYear, CorrelationId, CausationId`). The Core file moves from `ContestEnrichmentCompleted.cs` → `ContestFinalized.cs`.

### D2. Schedule the enqueue with a 30-second delay

When Producer's new handler receives `ContestCompleted`, the `DocumentRequested(Event)` re-source published on the same code path is still flowing through Provider → fetch → publish → Producer consumer → status processor. The `CompetitionStatus` row may not yet show `STATUS_FINAL`. Enrichment currently bails cleanly when status isn't final.

Defer with `IProvideBackgroundJobs.Schedule<IEnrichContests>(p => p.Process(cmd), TimeSpan.FromSeconds(30))` rather than `Enqueue`. 30 s is a best-guess that comfortably exceeds the typical re-source roundtrip in prod. The cron backstop (D3) covers any case where 30 s isn't enough.

The delay constant lives in the handler file as a `private const int EnrichmentDelaySeconds = 30;`. Configurable later via AppConfig if needed — not initially.

### D3. Keep `ContestEnrichmentJob` cron as permanent backstop

Mirrors the scoring backstop pattern from PR #382. Cron catches:
- Events lost in transit (broker outage, pod restart between publish and consume)
- Contests where the 30 s delay wasn't enough (slow re-source, ESPN latency)
- Manual replays / admin endpoints

Current `BackfillCurrentSeason = true` flag stays on for MLB through the season; flip off post-season but keep the cron alive. Cron's query filter `c.FinalizedUtc == null` already gives idempotency at the enqueue layer.

### D4. Add idempotency short-circuit on `Contest.FinalizedUtc != null`

Add at the top of both `BaseballContestEnrichmentProcessor.Process` and `FootballContestEnrichmentProcessor.Process`:

```csharp
if (contest.FinalizedUtc != null)
{
    _logger.LogInformation(
        "Contest already finalized. Skipping. ContestId={ContestId}, FinalizedUtc={FinalizedUtc}",
        command.ContestId, contest.FinalizedUtc);
    return;
}
```

Mirrors `ContestScoringProcessor.Process`'s "already scored, skip" guard. The cron's WHERE clause already filters these out, but event-driven retriggers, admin endpoint calls, and at-least-once redelivery don't. Belt-and-suspenders.

Note: the `contest` variable is currently loaded via `competition.Contest` after the `Competition` query — the short-circuit needs `contest` materialized, so it lands after the existing competition load, not at the very top.

## File-by-file plan

| File | Change |
|---|---|
| `src/SportsData.Core/Eventing/Events/Contests/ContestEnrichmentCompleted.cs` | **Delete** |
| `src/SportsData.Core/Eventing/Events/Contests/ContestFinalized.cs` | **New** — identical record shape, renamed |
| `src/SportsData.Producer/Application/Contests/BaseballContestEnrichmentProcessor.cs` | Publish `ContestFinalized` (was `ContestEnrichmentCompleted`); add D4 short-circuit |
| `src/SportsData.Producer/Application/Contests/FootballContestEnrichmentProcessor.cs` | Publish `ContestFinalized` (was `ContestEnrichmentCompleted`); add D4 short-circuit |
| `src/SportsData.Producer/Application/Events/ContestCompletedHandler.cs` | **New** — thin shim: receives `ContestCompleted`, calls `Schedule<IEnrichContests>(p => p.Process(cmd), 30s)` |
| `src/SportsData.Producer/Program.cs` | Add `typeof(ContestCompletedHandler)` to the consumers list (line 121) |
| `src/SportsData.Api/Application/Events/ContestCompletedHandler.cs` | **Delete** |
| `src/SportsData.Api/Application/Events/ContestFinalizedHandler.cs` | **New** — same shim shape as old ContestCompletedHandler: receives `ContestFinalized`, enqueues `ScoreContestCommand` |
| `src/SportsData.Api/Program.cs` | Replace `typeof(ContestCompletedHandler)` with `typeof(ContestFinalizedHandler)` (line 230) |
| Tests: `test/unit/SportsData.Producer.Tests.Unit/...` | Add `ContestCompletedHandlerTests` (asserts `Schedule` called with 30 s) + enrichment processor short-circuit tests |
| Tests: `test/unit/SportsData.Api.Tests.Unit/...` | Rename existing `ContestCompletedHandlerTests` → `ContestFinalizedHandlerTests`; update event type |

Total: ~10 file touches. No DB migration. No DTO/contract change.

## Sequencing & deploy considerations

**Event rename safety.** `ContestEnrichmentCompleted` currently has zero consumers in the codebase (verified by grep). Any in-flight messages of the old type at deploy time would have no consumer to dispatch to even *before* the rename — they're already invisible. After the rename, publishers emit only `ContestFinalized`. Zero risk of dropping legitimate work.

**Cross-service deploy order matters.** Producer and API are coupled through the renamed event and the handler relocation; rollout sequence determines whether scoring stays continuous or pauses during the deploy window.

- **Producer first (required):** Producer starts publishing `ContestFinalized`. API still has the old `ContestCompletedHandler` running and continues to score immediately on `ContestCompleted` (the bug, unchanged). New `ContestFinalized` messages accumulate in API's queue waiting for the new consumer — no work lost. Scoring stays continuous in its buggy form until the API deploy, then the new flow takes over.
- **API first (must avoid):** API starts consuming `ContestFinalized`, but Producer hasn't published any yet. The old `ContestCompletedHandler` is gone, so `ContestCompleted` no longer triggers scoring — *scoring pauses entirely* until Producer deploys and begins publishing `ContestFinalized`.

**Required sequence:** **Producer first, then API.** Producer-first keeps scoring continuous through the deploy window; API-first creates a scoring outage that lasts until the Producer deploy completes.

**Rollback:** Revert both PRs simultaneously. The system returns to the buggy-but-functional baseline. No data shape changes; rollback is purely code.

## Test plan

- [ ] `Producer.ContestCompletedHandler` unit test: AutoMocker; assert `Schedule<IEnrichContests>` invoked with a `TimeSpan` matching `EnrichmentDelaySeconds`.
- [ ] `Baseball|FootballContestEnrichmentProcessor` short-circuit tests: seed a `Contest` with `FinalizedUtc != null`, assert no `_bus.Publish` and no field writes.
- [ ] `API.ContestFinalizedHandler` unit test: rename of the existing `ContestCompletedHandler` test, swap event type and verify `IScoreContests.Process` enqueued.
- [ ] Manual E2E (local): copy prod MLB DB to local, replay one finalized contest's `ContestCompleted`, observe the 30 s delay → enrichment → `ContestFinalized` → scoring → correct results.
- [ ] Prod monitoring post-deploy: Seq queries to confirm
  - `Producer.ContestCompletedHandler` "scheduled enrichment" log fires on live game finals
  - `Producer.{Sport}ContestEnrichmentProcessor` "published ContestFinalized" log fires ~30 s later
  - `API.ContestFinalizedHandler` "enqueued ScoreContestCommand" log fires
  - `Contest.FinalizedUtc != null` before any `PickemGroupUserPick.ScoredAt`

## Out of scope

- **Replacing `MatchupResult` HTTP roundtrip with the event payload.** The current API scoring path still calls Producer's `GetMatchupResult` after consuming `ContestFinalized` — could be streamlined by carrying the resolved winner/scores/odds on the event itself, eliminating the HTTP fetch. Worth doing separately if the HTTP hop becomes a bottleneck.
- **Listening on `CompetitionStatus` document-created events** instead of guessing a 30 s delay. Architecturally cleaner but bigger surface area; defer until the 30 s heuristic proves brittle.
- **Removing the `ContestCompleted` event entirely.** It still has legitimate non-scoring downstream uses (e.g. SignalR live-state notifications, `*PlayCompleted` semantics). Keep it as a lifecycle signal; just don't trigger scoring off it.
- **Removing the `BackfillCurrentSeason` flag on the cron.** Tied to MLB mid-season onboarding; lifecycle of that flag is a separate decision from this restructure.

## Open items

- Confirm `IProvideBackgroundJobs.Schedule<T>(...)` signature matches the call site in the new handler (verified earlier in the codebase — exists at `BackgroundJobProvider.cs:50`).
- Confirm `Producer.Application.Events/` is the right home for the new `ContestCompletedHandler` (mirrors API's structure and `OutboxTestEventHandler.cs` already lives there).
- `event-surface-overview.md` will drift further out of date after this change. Not blocking; flagged for the next time that doc gets refreshed.
