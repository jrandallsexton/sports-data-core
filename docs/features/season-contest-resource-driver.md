# By-Season Contest Re-Source Driver

Status: **Implemented** (PR #550) — `RefreshContestsBySeasonYearHandler`, the
`POST /api/contests/refresh` endpoint, DI + validator registrations, and unit tests.
Last updated: 2026-07-22

## Goal

A single operation that re-sources **every contest for a given `(sport, seasonYear)`**
through the existing narrowed "Refresh Contest" path — to backfill point-in-time
records (`CompetitionCompetitorRecord`) and other per-contest documents without
dragging in athletes/rosters/etc.

Motivating case: NCAA 2025 point-in-time records. Validated manually by hitting
the admin "refresh contest" button on every LSU 2025 game and watching the
records appear on the picks page. This driver is "do that, for a whole season."

## Why none of the existing options fit

| Existing | What it does | Why not |
|---|---|---|
| `POST /api/sourcing/historical/seasons` (Provider) | Full historical sourcing | Too broad — pulls athletes and everything |
| `POST /api/contests/{contestId}/update` (the admin button) | Per-contest refresh → `UpdateContestCommand` → narrowed `ContestRefreshDocumentTypes` | Correct mechanism, but one contest at a time |
| `POST /api/contests/finalize` (`FinalizeContestsBySeasonYear`) | By-season, but **finalizes** (winner/scores/enrichment) | Not a re-source of the record/play documents |
| `ContestUpdateJob.ExecuteBackfillCurrentSeason` | Fans out the refresh per contest for a season | **Current season only**, filters `FinalizedUtc == null` (excludes finished games — the exact ones we need for 2025 records), flag-gated |

There is **no** ESPN resource index listing every contest for a season, so this
can't be kicked off from Provider — it must start from Producer's canonical
contest data.

## Key realization: the refresh path already IS "grab ExternalId → request sourcing"

`ContestUpdateProcessor.ProcessInternal` already does exactly the "grab the
`ContestExternalId`, request sourcing for that URI" flow — with the narrowing
that excludes athletes:

```csharp
var contestExternalId = contest.ExternalIds.FirstOrDefault(x => x.Provider == provider);
var contestIdentity = _externalIdentityGenerator.Generate(contestExternalId.SourceUrl);
await _bus.Publish(new DocumentRequested(
    Uri: new Uri(contestIdentity.CleanUrl),
    DocumentType: DocumentType.Event,
    SeasonYear: contest.SeasonYear,
    IncludeLinkedDocumentTypes: ContestRefreshDocumentTypes));   // Event, EventCompetition, Status,
                                                                 // Situation, Broadcast, Odds, Competitor,
                                                                 // CompetitorScore/LineScore/Record,
                                                                 // Play, Drive, Leaders, Probability
```

So the driver does **not** replicate the external-id lookup or the
`DocumentRequested` construction. It just needs the list of **distinct
ContestIds** and enqueues `UpdateContestCommand` per contest — the same command
the admin button fires.

## Approach

Producer-side. For a given `(sport, seasonYear)`:

1. Distinct ContestIds via the **FranchiseSeason traversal** (chosen over
   `Contests.Where(SeasonYear == year)` deliberately, to avoid leaning on the
   `Contest.SeasonYear` denormalization — see `project_seasonyear_denorm`):
   ```
   FranchiseSeason (SeasonYear == year)  →  their Ids
     → CompetitionCompetitor (FranchiseSeasonId ∈ those)   // indexed on FranchiseSeasonId
     → Competition.ContestId
     → Distinct()                                          // each contest has 2 franchise seasons — DEDUP
   ```
   `FranchiseSeason` has no direct competition/contest navigation, so the hop is
   through `CompetitionCompetitor.FranchiseSeasonId → Competition.ContestId`.
   **The `Distinct()` is load-bearing** — home + away each reference the same
   contest; without it every contest enqueues twice.
2. For each distinct ContestId:
   `Enqueue<IUpdateContests>(p => p.Process(new UpdateContestCommand(contestId, Espn, sport, correlationId)))`.
3. `ContestUpdateProcessor` does the narrowed sourcing request per contest;
   Provider sources it (serving from Mongo where cached, ESPN where not).

### Throttling: none needed

The Redis **token bucket** (ESPN circuit breaker / rate limiter) meters ESPN
load, so the driver can enqueue all contests at once without staggering.

### Cache reality

The Mongo document cache is **partial** — *some* contests have plays/records in
Mongo, many (e.g. most of NCAA 2025) do not (they were never live-captured; and
some were lost in the `data-0` drive failure). The refresh serves cache hits
where available and crawls ESPN for the gaps; the token bucket keeps that safe.
See [[project_mongo_cache_gap_historical_ncaa]].

## Build plan

- **Endpoint** — `POST /api/contests/refresh` on the Producer `ContestController`,
  body `{ Sport, SeasonYear }`, returns `Accepted` + correlationId. Mirrors the
  existing `finalize` endpoint's shape and background-enqueue pattern.
- **Handler** — `IRefreshContestsBySeasonYearHandler` background job: runs the
  FranchiseSeason→distinct-ContestId traversal, enqueues `UpdateContestCommand`
  per contest, logs the enqueued count.
- **Test** — given franchise seasons that share competitions for a year, the
  handler enqueues each contest **exactly once** (proves the dedup).

Reuses the proven refresh path end-to-end; the only new code is the
season → distinct-ContestId fan-out.

## Open option (not decided)

**Contest scope.** Default = *all* contests for the season. A smarter variant
would target only contests **missing canonical records** (drive off a
coverage query like `sql/pgsql/_debug_point_in_time_coverage.sql`) so already-
complete games aren't re-refreshed — smallest ESPN footprint, self-limiting.
Ship the simple "all" version first; add the gap-filter later if the footprint
matters.

## Related

- docs/features/point-in-time-team-records.md — the "Sourcing" section this feeds.
- docs/processing/include-linked-document-types.md — the narrowing mechanism.
- [[project_mongo_cache_gap_historical_ncaa]] — why NCAA 2025 largely hits ESPN.
- [[project_seasonyear_denorm]] — why the traversal avoids `Contest.SeasonYear`.
