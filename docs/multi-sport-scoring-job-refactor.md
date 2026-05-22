# Multi-Sport Scoring Refactor

**Status:** Draft for review. No code yet.
**Owner:** sports-data-core
**Created:** 2026-05-22
**Related code:** `src/SportsData.Producer/Application/Competitions/CompetitionStreamerBase.cs`, `src/SportsData.Api/Application/Jobs/*`, `src/SportsData.Api/Application/Scoring/*`

## Problem

Three recurring jobs in `SportsData.Api` plus the per-contest `ContestScoringProcessor` were built last season for `Sport.FootballNcaa` and hardcode that sport when resolving sport-routed clients:

| File | Lines | What's hardcoded |
|---|---|---|
| `Jobs/LeagueWeekScoringJob.cs` | 45, 74 | `_seasonClientFactory.Resolve(Sport.FootballNcaa)`, `_contestClientFactory.Resolve(Sport.FootballNcaa)` |
| `Jobs/ContestScoringJob.cs` | 52, 71 | Same pattern |
| `Jobs/MatchupScheduler.cs` | 36 | `_seasonClientFactory.Resolve(Sport.FootballNcaa).GetCurrentSeasonWeek()` |
| `Scoring/ContestScoringProcessor.cs` | 43 | `_contestClientFactory.Resolve(Sport.FootballNcaa).GetMatchupResult(contestId)` |

Each has a `// TODO: multi-sport` comment.

Underneath the hardcoded sport, the bigger architectural issue is that scoring today is **pull-based** — the cron polls every week (currently `Cron.Weekly` for all four jobs in `DependencyInjection/ServiceRegistration.cs:272-292`) asking Producer "is anything finalized yet?" — instead of being **push-based** from the streamer that already knows the moment a game ends.

## What's already in place

- **`PickemGroup.Sport`** — required, populated at league creation.
- **`Contest.Sport`** (API-side) — required, populated when contest is recorded.
- **`CompetitionStreamerBase` already detects `STATUS_FINAL`** in three places, each currently only flipping `CompetitionStream.Status` to `Completed` without publishing a domain event:
  - `:208` — `LiveStartOutcome.AlreadyFinal` (game was final before we started watching)
  - `:231` — initial status switch (game was final on our first status check)
  - `:267` — `PollOutcome.Final` from in-game polling (game went final mid-watch)
- **`ContestScoringProcessor` already triggers league week scoring** at the tail (`:151`) — picks scoring naturally chains into leaderboard scoring within the same processor invocation. No separate orchestration needed.
- **The downstream scoring math** (`LeagueWeekScoringService`, `PickScoringService`) is fully sport-agnostic — operates on generic `PickemGroup*` entities that carry `Sport` as data rather than branching on it in code.

So we are not missing data and not missing chaining. We are missing the trigger and the sport-correct factory resolution.

## Recommendation: event-driven primary, daily cron backstop

### Primary path (live)

```
Streamer detects STATUS_FINAL
  → publishes ContestCompleted (new event)
  → API consumer enqueues ContestScoringProcessor
  → ContestScoringProcessor scores picks (using contest.Sport, not hardcoded)
  → tail invokes LeagueWeekScoringService.ScoreLeagueWeekAsync
  → leaderboard updates
```

Latency from game end to leaderboard update: seconds to single-digit minutes (bounded by the in-game polling interval + MassTransit delivery).

### Backstop path (catch-up)

A single daily cron pass for each of `ContestScoringJob` and `LeagueWeekScoringJob` looks for unscored picks against finalized contests and processes them. This catches:
- Events lost to RabbitMQ outage or consumer pod restart during the broadcast
- Contests finalized via paths other than the streamer (admin replay, bulk finalize)
- Anything else the primary path missed

The backstop is sport-agnostic: querying `UserPicks.Where(ScoredAt == null)` against the list of finalized contests doesn't require sport-specific factory resolution at the job level — `ContestScoringProcessor` resolves sport per-contest.

### Why this is simpler than per-sport subclasses

With the primary path live-driven, the cron's job collapses to "find anything that slipped through" instead of "iterate season weeks, resolve current week, fetch finalized contests, cross-reference." No sport-specific week semantics. No per-sport cadence. No abstract-base / concrete-per-sport class hierarchy. A single daily run across all sports is sufficient because the live path already handles the time-sensitive case.

## Concrete changes

### 1. `ContestCompleted` domain event (new)

New record in `SportsData.Core.Eventing.Events.Contests` (or sibling location). At minimum:

```csharp
public record ContestCompleted(
    Guid ContestId,
    Guid CompetitionId,
    Sport Sport,
    int SeasonYear,
    Guid? SeasonWeekId,
    Guid CorrelationId,
    Guid CausationId
);
```

Sport is on the event so the consumer can route without a DB lookup. SeasonYear and SeasonWeekId help the consumer make idempotency / locality decisions if needed.

### 2. `CompetitionStreamerBase` — publish at the three completion sites

All three sites listed under "What's already in place" publish `ContestCompleted` before (or after — order doesn't matter; both happen in the same SaveChangesAsync transaction with bus-outbox semantics) the `UpdateStreamStatusAsync(stream, Completed, ...)` call. Includes the existing log scope (Sport, ContestId, CompetitionId, CorrelationId) so the publish carries clean context.

At-least-once delivery means the consumer may see `ContestCompleted` multiple times per contest (e.g., admin re-broadcasts a completed game). The consumer must be idempotent.

### 3. API consumer for `ContestCompleted` (new)

A MassTransit consumer in `SportsData.Api` that receives `ContestCompleted` and enqueues `IScoreContests.Process(new ScoreContestCommand(ContestId))` via the existing `IProvideBackgroundJobs`. Thin shim per the codebase convention — no DB work inline.

Idempotency: if the contest is already fully scored, the existing `ContestScoringProcessor` should short-circuit. Today it does not short-circuit — it re-iterates all `UserPicks` for the contest. Add an "already-scored" guard inside the processor in this PR (cheap check: `UserPicks.Any(p => p.ContestId == X && p.ScoredAt == null)`).

### 4. `ContestScoringProcessor` — resolve sport from the contest

```csharp
// Before
var matchupResultResponse = await _contestClientFactory
    .Resolve(Sport.FootballNcaa)
    .GetMatchupResult(command.ContestId);

// After
var contest = await _dataContext.Contests.AsNoTracking()
    .FirstOrDefaultAsync(c => c.ContestId == command.ContestId);
if (contest is null) { /* log + return */ }
var matchupResultResponse = await _contestClientFactory
    .Resolve(contest.Sport)
    .GetMatchupResult(command.ContestId);
```

Two new lines. The processor remains a single class — sport is data, not a class-level distinction.

### 5. `ContestScoringJob` — sport-agnostic daily backstop

Rewrite to:

```
For each Sport in {FootballNcaa, BaseballMlb, ...}:
    finalizedIds = _contestClientFactory.Resolve(Sport).GetFinalizedContestIds(...)
    unscoredContestIds = UserPicks.Where(ScoredAt == null).Select(ContestId).Distinct()
    For each contest in (unscored ∩ finalized):
        Enqueue ContestScoringProcessor
```

Discover the active sport list at runtime from `PickemGroups.Select(g => g.Sport).Distinct()` so adding a new sport doesn't require touching this job. Daily cadence.

### 6. `LeagueWeekScoringJob` — sport-agnostic daily backstop

Rewrite to:

```
unscoredLeagueWeeks = PickemGroupMatchups
    .Join(PickemGroupWeekResults — null OR stale, i.e. older than max(UserPick.ScoredAt))
    .Select(GroupId, SeasonYear, SeasonWeek)
For each: ScoreLeagueWeekAsync
```

The exact predicate is: a league week needs scoring if any pick for that league+year+week was scored after the last `PickemGroupWeekResult.CalculatedUtc` for that league+year+week (or if no result row exists). This catches league weeks where contest scoring succeeded but the tail leaderboard scoring failed.

### 7. `MatchupScheduler` — loop over sports

MatchupScheduler is not in the eventing path (matchups must be generated *before* games happen). Rewrite the body to iterate over the active sport list and call the per-sport `GetCurrentSeasonWeek()`. Single job, daily cadence, no subclass hierarchy.

## Cron schedule

All four jobs become daily. Currently `Cron.Weekly` placeholders.

| Job | Cadence | Role |
|---|---|---|
| MatchupScheduler | Daily 06:00 UTC | Primary — schedules upcoming matchups |
| ContestScoringJob | Daily 09:00 UTC | Backstop — catches contests where ContestCompleted was missed |
| LeagueWeekScoringJob | Daily 09:15 UTC | Backstop — catches league weeks where contest-tail leaderboard scoring failed |

15-min stagger between ContestScoringJob and LeagueWeekScoringJob is best-effort. Both are self-correcting.

## Migration path

Each step is shippable independently and exercises a piece of the architecture before the next builds on it.

1. **PR-N+0 (this doc)** — design doc only.
2. **PR-N+1** — `ContestScoringProcessor.cs:43` uses `contest.Sport`. Add the "already scored" short-circuit guard. Small, unblocks both event-driven and cron-driven paths.
3. **PR-N+2** — define `ContestCompleted` event. Publish from the three `CompetitionStreamerBase` sites. **No consumer yet** — publishing into the void is safe (RabbitMQ retains until consumed; if no consumer is registered, the event is discarded per MassTransit's default behavior, which is what we want until step 3).
4. **PR-N+3** — API `ContestCompleted` consumer that enqueues `ContestScoringProcessor`. Closes the live-scoring loop. Verify against an MLB contest finalizing.
5. **PR-N+4** — rewrite `ContestScoringJob` + `LeagueWeekScoringJob` as sport-agnostic daily backstops. Both files change in one PR (similar shape, similar testing). Bump Hangfire cron to daily.
6. **PR-N+5** — rewrite `MatchupScheduler` to loop over sports. Separate from scoring; can land before or after step 4 without coupling.

## Open questions

1. **Where to source the active sport list at runtime** — `PickemGroups.Select(g => g.Sport).Distinct()` is the data-driven option. Alternative: hardcode a small static `[FootballNcaa, BaseballMlb]` list and update on each sport launch. Recommendation: data-driven. New sports get picked up the moment their first league is created.

2. **Should the "already scored" short-circuit also live in the consumer, or only in the processor?** Recommendation: only in the processor. Keeping the guard at the lowest level means both event and cron triggers benefit, and the consumer stays trivially thin.

3. **`ContestCompleted` payload — anything beyond ContestId + Sport + ids needed?** The consumer currently only needs `ContestId` to enqueue the processor (which then fetches everything else). Including SeasonYear / SeasonWeekId is cheap and helps tracing.

## Out of scope

- The scoring math itself. `LeagueWeekScoringService.ScoreLeagueWeekAsync` and `PickScoringService.ScorePick` stay as-is.
- KEDA / pod-count tuning.
- Game Center wallboard work — separate effort.
- Per-contest sport-routing for any other consumer (e.g., admin replay). If those grow `// TODO: multi-sport` markers, same `contest.Sport` lookup pattern applies; not blocking on this refactor.
