# Multi-Sport Scoring Job Refactor

**Status:** Draft for review. No code yet.
**Owner:** sports-data-core
**Created:** 2026-05-22
**Related code:** `src/SportsData.Api/Application/Jobs/*`, `src/SportsData.Api/Application/Scoring/*`

## Problem

Four recurring jobs in `SportsData.Api` were built last season for `Sport.FootballNcaa` and hardcode that sport when resolving sport-routed clients. With MLB live and NFL on deck (and more sports eventually), the current shape produces wrong behavior for non-football leagues:

| File | Lines | What's hardcoded |
|---|---|---|
| `Jobs/LeagueWeekScoringJob.cs` | 45, 74 | `_seasonClientFactory.Resolve(Sport.FootballNcaa)`, `_contestClientFactory.Resolve(Sport.FootballNcaa)` |
| `Jobs/ContestScoringJob.cs` | 52, 71 | Same pattern |
| `Jobs/MatchupScheduler.cs` | 36 | `_seasonClientFactory.Resolve(Sport.FootballNcaa).GetCurrentSeasonWeek()` |
| `Scoring/ContestScoringProcessor.cs` | 43 | `_contestClientFactory.Resolve(Sport.FootballNcaa).GetMatchupResult(contestId)` |

Each is annotated with a `// TODO: multi-sport` comment. Downstream of these calls, `LeagueWeekScoringService`, `PickScoringService`, and the `PickemGroup*` data layer are already sport-agnostic — they operate on entities that carry `Sport` as data, not as a code-path branch.

## What's already sport-tagged at the data layer

- `PickemGroup.Sport` — **required**, populated at league creation.
- `Contest.Sport` (API-side) — populated when contest is recorded.
- `PickemGroupMatchup` — joins to `PickemGroup`, so league sport is one join away.

So we are not missing data. We are just not reading it.

## Why this isn't a one-line factory swap

Four concerns make a "loop over all sports inside the existing single job" approach awkward:

1. **Cadence differs significantly.**
   - Football: most contests finalize Sunday night. Hourly during weekends is plenty; daily otherwise is fine.
   - Baseball: contests finalize nightly, ~15 games/day. Daily-or-better is the floor; every 30 minutes during the in-season evening window is more appropriate.
   - Today all four jobs are registered as `Cron.Weekly` in `ServiceRegistration.cs:272/277/282/292`, which is a placeholder pace inherited from the football-only world and is too slow even for football.

2. **"Season week" semantics differ.**
   - Football: weeks 1–15+ with well-defined calendar boundaries, NCAAFB has bowl postseason which uses non-standard-week ordinals.
   - Baseball: custom date-range windows. The mobile `League.seasonWeeks` JSDoc explicitly notes *"Custom-window leagues may contain a subset (e.g. [4]) rather than 1..N."*
   - A single `_seasonClientFactory.Resolve(Sport).GetCurrentAndLastSeasonWeeks()` returns very different shapes per sport. There's no clean way to fold them into one loop without sport-conditional logic.

3. **Season calendars overlap, sometimes don't.**
   - Spring/early summer: MLB only.
   - Fall: MLB tail + NCAAF + NFL.
   - Winter: NCAAF bowls + NFL playoffs.
   - A single job iterating over all sports has to coordinate three independent "what's active right now?" answers and gracefully skip sports out of season.

4. **Failure isolation matters in production.**
   - If MLB's Producer hangs on a 100s HttpClient.Timeout (we saw this on 2026-05-22, PR #354), football scoring should not block behind it.
   - Per-sport jobs fail per-sport; one job per all sports cascades the blast radius.

## Recommendation: sport-specific subclasses with shared sport-agnostic core

Three flavors of change. None of them touch the actual scoring math.

### 1. `ContestScoringProcessor` — resolve sport from the contest itself

This processor runs once per `contestId`. The contest knows its own sport (`Contest.Sport` in `AppDataContext.Contests`). No subclass needed:

```csharp
// Before
var matchupResultResponse = await _contestClientFactory
    .Resolve(Sport.FootballNcaa)
    .GetMatchupResult(command.ContestId);

// After
var contest = await _dataContext.Contests.AsNoTracking()
    .FirstOrDefaultAsync(c => c.ContestId == command.ContestId);
if (contest is null)
{
    _logger.LogWarning("Contest not found for scoring: {ContestId}", command.ContestId);
    return;
}
var matchupResultResponse = await _contestClientFactory
    .Resolve(contest.Sport)
    .GetMatchupResult(command.ContestId);
```

Pure refactor. One method, two new lines, no new file.

### 2. `LeagueWeekScoringJob` + `ContestScoringJob` + `MatchupScheduler` — sport-specific subclasses

Extract sport-neutral logic into an abstract base (or a shared service consumed by thin wrappers — open question, see §"Naming and shape" below). Concrete types per sport:

```
ContestScoringJobBase            (abstract)
├── FootballNcaaContestScoringJob
└── BaseballMlbContestScoringJob

LeagueWeekScoringJobBase         (abstract)
├── FootballNcaaLeagueWeekScoringJob
└── BaseballMlbLeagueWeekScoringJob

MatchupSchedulerBase             (abstract)
├── FootballNcaaMatchupScheduler
└── BaseballMlbMatchupScheduler
```

Each concrete type:
- Declares `protected abstract Sport Sport { get; }`
- Inherits the orchestration loop from the base
- Is registered as its own Hangfire recurring job with its own cron

When NFL goes live: add `FootballNflContestScoringJob` (and friends). When NBA: same pattern. The growth shape is linear.

### 3. Sport-filter the matchup queries

Both job bases currently query `PickemGroupMatchups.Where(m => SeasonYear == X && SeasonWeek == Y)` — this picks up *all* leagues regardless of sport. Add the filter:

```csharp
var leagueWeeks = await _dataContext.PickemGroupMatchups
    .Where(m => m.SeasonYear == seasonWeek.SeasonYear
             && m.SeasonWeek == seasonWeek.WeekNumber)
    .Join(_dataContext.PickemGroups,
        m => m.GroupId,
        g => g.Id,
        (m, g) => new { m, g.Sport })
    .Where(x => x.Sport == Sport) // ← the per-sport filter from the subclass
    .Select(x => new { x.m.GroupId, x.m.SeasonYear, x.m.SeasonWeek })
    .Distinct()
    .ToListAsync();
```

Without this filter, the baseball job would process football leagues' matchups (and vice versa), applying the wrong week semantics in the process.

## Cron schedule (proposed)

Today all four jobs are `Cron.Weekly` — wrong for both sports. Proposed:

| Job | Football (NCAA/NFL) | Baseball (MLB) | Rationale |
|---|---|---|---|
| MatchupScheduler | `0 6 * * 2` (Tue 06:00 UTC = Mon evening US) | `0 9 * * *` (daily 09:00 UTC = early-morning US) | Football schedule firms up early week; MLB schedules game-by-game. |
| ContestScoringJob | `0 */2 * * 0` (every 2h on Sundays) + `0 6 * * 1` (Mon 06:00 UTC catch-up) | `0 6,15 * * *` (06:00 UTC + 15:00 UTC) | Football: most finalize during US Sunday afternoon/evening. Baseball: most finalize US evening into early morning. |
| LeagueWeekScoringJob | Same as ContestScoringJob + 15 min offset | Same + 15 min offset | Runs after ContestScoringJob so picks are scored before leaderboards aggregate. |

The 15-min offset between ContestScoringJob and LeagueWeekScoringJob is best-effort, not strict — the LeagueWeekScoringJob already has self-correcting "score if not scored recently" logic, so even if it runs first it'll backfill on the next pass.

NFL crons are identical to NCAA initially — NFL games run Thursday/Sunday/Monday so the Sunday-anchored schedule fits both.

Open question on whether MLB needs in-game scoring cadence (every 30 min during games) vs the once-per-day model proposed here. The once-per-day model is sufficient for next-day leaderboard freshness but won't update live during a game. Given baseball picks are typically a series-level commitment (not in-game), once-per-day is probably fine for Phase 1.

## Naming and shape

Two structural choices to make before coding. Both are deferred to the implementation PR — flagging them here for awareness.

### Choice A: prefix vs suffix

- `FootballNcaaContestScoringJob` (sport-as-prefix)
- `ContestScoringJobFootballNcaa` (sport-as-suffix)

Codebase has both patterns: `BaseballEventCompetitionPlayDocumentProcessor` uses sport-as-prefix; `MatchupForPickDtoMapper` uses no sport tag at all. Prefix reads more naturally in alphabetical file listings (all football files cluster). Recommendation: **prefix**, pending user confirmation.

### Choice B: abstract base class vs shared service

- **Abstract base.** `ContestScoringJobBase` holds the orchestration loop; concrete subclasses override `Sport` and the per-sport `GetCurrentWeekAsync` virtual.
- **Shared service.** Thin sport-specific job wrappers (`FootballNcaaContestScoringJob` is a 10-line class that calls `_scoringWorkflow.RunAsync(Sport.FootballNcaa)`). The workflow object holds the logic.

Both are valid. Abstract base is the more conventional pattern and matches existing `EventCompetitionPlayDocumentProcessorBase<TDataContext, TDto>`. Shared service is friendlier to unit testing and avoids inheritance constraints. Recommendation: **abstract base**, mirroring the processor-base pattern already in the codebase.

## Migration path

Suggested order, each step shippable independently:

1. **PR-N+0 (this doc)** — design doc only. Land for review.
2. **PR-N+1** — refactor `ContestScoringProcessor.cs:43` to use `contest.Sport`. Independent of the job split; unblocks per-contest sport handling.
3. **PR-N+2** — introduce `ContestScoringJobBase` + `FootballNcaaContestScoringJob`. Register new job with football cadence. Delete the old `ContestScoringJob`. Verify scoring still works for the active football season.
4. **PR-N+3** — same shape for `LeagueWeekScoringJob`. Verify leaderboards still update for football.
5. **PR-N+4** — same shape for `MatchupScheduler`. Verify matchup generation still produces football matchups on the new cadence.
6. **PR-N+5** — add `BaseballMlbContestScoringJob`, `BaseballMlbLeagueWeekScoringJob`, `BaseballMlbMatchupScheduler`. Verify against an existing MLB league with finalized contests.

Steps 3–5 are deliberately one-sport-at-a-time so we can verify the refactor didn't regress football before adding baseball. The base class is fully exercised by step 3 — adding baseball in step 6 is then a confidence check, not a discovery exercise.

## Open questions for user

1. **MLB cadence** — once-daily (proposed) or in-game polling (every 30 min during active games)? Picks-product question, not implementation.
2. **Prefix vs suffix naming** — recommendation is prefix; user override?
3. **Abstract base vs shared service** — recommendation is abstract base; user override?
4. **Should MatchupScheduler also split now**, or defer until baseball matchup generation is needed? Today MatchupScheduler only produces football matchups; baseball picks may use a different generation path entirely (e.g., manual commissioner setup for series-based leagues). Need to confirm baseball league shape before deciding.

## Out of scope

- The scoring math itself. `LeagueWeekScoringService.ScoreLeagueWeekAsync` and `PickScoringService.ScorePick` stay as-is — they're sport-agnostic and operate on already-finalized data.
- KEDA / pod-count tuning. These jobs run inside the existing API pod; no infrastructure change.
- Game Center wallboard work. The runner-on-base data populated for the Game Center vision is unrelated to scoring cadence; tracked separately.
