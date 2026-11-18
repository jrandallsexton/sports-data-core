# Splits data audit (2026-11-17)

Read-only checks of `FranchiseContestFact` (NCAAFB and NFL, prod) after the
2001-2025 backfill and the 2016/2017 NCAA re-sources. Two open problems:
**duplicate NCAA games** (needs a design decision) and **Louisiana Tech's 2026
conference** (needs a re-source and rebuild).

## Context: what is already done

| Item | Result |
|---|---|
| NCAA fact backfill 2001-2025 | Done 11-16. |
| NCAA 2016 re-source + fact rebuild | 873 FBS games (was 726), 0 record mismatches. |
| NCAA 2017 re-source + fact rebuild | 874 FBS games (was 825), 0 record mismatches. 6 FCS-only contests still have no Competition (ignored). |
| NFL fact backfill 2001-2025 | Done 11-16. |
| NFL 2004 (49ers at Saints) and 2010 (Giants at Texans) | Re-sourced per contest via `contests/{id}/update`, then 2004 and 2010 rebuilt. |
| NCAA 2000 | Skipped by decision. ESPN publishes no 2000 conference tree, no `curatedRank` and no kickoff times. |
| Splits conference filter floor (#870) | Lowered 2014 to 2001. Live after the cluster credential fix (see below). |

Season re-source lesson: including `EventCompetitionPlay` in a season
re-source multiplied the Provider's work (about 550k jobs, about 9 hours for
2017). Leave plays out unless the season's plays are missing.

## Check 1: record going in (WinsBefore / LossesBefore)

Method: for each team-season, recompute wins and losses before each game from
the fact rows ordered by `KickoffUtc`, and compare with the stored values.
FCS rows are excluded because FCS-vs-FCS games are not fact rows, so an FCS
team's record cannot be rebuilt from facts.

| Sport | Result |
|---|---|
| NFL 2001-2025 | 0 mismatches. |
| NCAA FBS 2001-2025 | 21 mismatches (2008: 3, 2012: 14, 2013: 4). |

All 21 NCAA mismatches are the duplicate games below: each affected team has
two fact rows with the same kickoff, so the check counts the duplicate as a
prior game.

That number is a lower bound, not a clean result. Check 1 recomputes from the
same fact rows, which include both copies of each duplicate, so it cannot see
the inflation the duplicates cause:

- The projector's record-going-in (`GetRecordsBeforeAsync`) counts every
  finalized, non-preseason contest with `StartDateUtc` before kickoff. A
  duplicate therefore adds one win or loss to `WinsBefore`/`LossesBefore` on
  every later game for both teams, and the recompute agrees with it.
- `EnrichFranchiseSeasonHandler` counts both copies too, so the team-season
  W-L (and conference W-L, for conference championship games) is overstated.

Outside the 22 affected team-seasons (2008, 2012, 2013), the records are
correct. For those teams, records are overstated until the duplicates are
resolved. The fix is the dedup decision in Check 2, then a fact rebuild and a
team-season re-enrich for 2008, 2012 and 2013.

## Check 2: duplicate NCAA games (OPEN, needs design)

ESPN publishes 11 NCAA games under two event ids: an older id (`28...`,
`32...`, `33...`, `34...`) and a newer `400...` id. Both were sourced as
separate `Contest` rows, both finalized, and both projected, so Splits counts
each of these games twice (inflating W-L, ATS, averages and favorite/underdog
counts for both teams). NFL has none.

Spot check: both ids of the 2012 SEC Championship (`400434780`, `323360061`)
and of 2008 Appalachian State at LSU (`282430099`, `400361387`) resolve on
ESPN's core API as the same game.

| Season | Game | ContestId (event) | ContestId (event) |
|---|---|---|---|
| 2008 | Appalachian State at LSU | `53476f4e-9b83-7014-bc27-6de6d9c5ea68` (282430099) | `85c4282c-f5b3-ff6e-c536-44771fb5e01b` (400361387) |
| 2008 | Arizona State at California | `a987cef9-eaaa-c7e3-b778-69541236e9b7` (282780025) | `bdcf5f31-031a-5923-162a-4316c25a31f2` (400361396) |
| 2012 | Temple at Army | `359b6774-3d7a-c6ad-9feb-302770b3a426` (323220349) | `cc04e7e4-fa8e-cd8e-6887-738eaf5df400` (400368353) |
| 2012 | Northern Illinois at Kent State (MAC CG) | `51144004-1ee5-6c33-f8af-cad5a9f1e1cb` (400433825) | `9ae817a3-51b7-f02c-98e8-49de1e2983fc` (323352309) |
| 2012 | UCLA at Stanford (Pac-12 CG) | `516a617a-4bdb-8f07-d238-338deb68583c` (400434781) | `da0666fd-66b0-db19-84d7-68c1cbdaeb30` (323350024) |
| 2012 | UCF at Tulsa (C-USA CG) | `35fca545-2241-e1a0-3c8a-6f5e95bf6297` (323360202) | `fe4245b2-1725-f854-cdf1-10e91b56f688` (400434775) |
| 2012 | Alabama at Georgia (SEC CG) | `810647ff-abba-631c-3473-f3422cc8f44c` (400434780) | `a2608d8f-0b0e-6034-c5ed-aef09cbd8d53` (323360061) |
| 2012 | Florida State at Georgia Tech (ACC CG) | `b55250a3-0f3b-86f1-7293-e1f1cec13bc5` (323360059) | `c9e8d90f-842b-d2a6-485e-654f60233fb8` (400434085) |
| 2012 | Nebraska at Wisconsin (Big Ten CG) | `23918ad7-3df0-6442-1d0e-eca1e38a6250` (400434615) | `44242433-c790-35ce-a38b-cabb30b3200b` (323360275) |
| 2013 | Louisiana at Tulane (New Orleans Bowl) | `06bcf05a-1884-4ff4-7cf1-aea51735da7a` (400520954) | `ac867d83-7381-00d9-c034-18f4496be099` (333550309) |
| 2013 | Nebraska at Georgia (Gator Bowl) | `03885751-571a-1970-6bd9-a74b0192e8e3` (340010061) | `5b356bb3-37f2-c2f6-eda1-1845db550547` (400521191) |

Detection (both members of a pair share teams and date):

```sql
select "FranchiseSeasonId", "OpponentFranchiseSeasonId", "KickoffUtc"::date, count(*), array_agg("ContestId")
from "FranchiseContestFact"
group by 1, 2, 3 having count(*) > 1;
```

### Open questions

- **Blast radius beyond facts.** The duplicates are `Contest` rows, so they
  double-count in team-season and conference W/L (`EnrichFranchiseSeasonHandler`
  counts both; confirmed in code), and may in athlete stats and leaders, metrics, and anything else
  that reads contests by season. Each consumer needs checking.
- **Which id to keep.** Is `400...` always ESPN's canonical id? Which of the
  pair carries the plays, odds (2012+), box stats and `curatedRank`? Keep the
  richer row, or always the `400...` row?
- **How to retire the other.** Options:
  - Mark it cancelled (`CancelledUtc`). The fact projection already skips
    cancelled contests, but "cancelled" is the wrong meaning for a game that
    was played, and other readers may not filter on it.
  - Delete it (and its Competition graph). Clean, but a later season re-source
    would recreate it from ESPN's listing.
  - Record it as a known alias (a superseded-by link), so re-sourcing
    recognizes it and skips it.
- **Prevention.** Detect at sourcing time (same two franchise-seasons, same
  date) and refuse or link the second contest, rather than cleaning up after.
- **Are there more?** This check only sees FBS games that reached the fact
  table. Run the same teams-plus-date check against `Contest` directly
  (including FCS-only and unfinalized contests), for NCAA, NFL and MLB.

Until resolved, Splits for the affected teams in 2008, 2012 and 2013 counts
these games twice.

## Check 3: 2026 conference flag vs conference membership (RESOLVED)

Resolved 2026-11-17: owner confirmed on ESPN that Louisiana Tech is in the
Sun Belt. The 2026 conference re-source moved it to `belt|beltw` (318
team-seasons changed; the only other FBS/FCS change was West Florida to FCS
UAC, the rest D2/D3), and the 2026 fact rebuild left 0 cross-conference
rows flagged as conference games. Louisiana Tech's six Sun Belt games are
now conference games in Splits.

Original finding:

12 fact rows in 2026 are flagged as conference games between teams in
different conferences. All are Louisiana Tech's six 2026 games against Sun
Belt teams (Louisiana, UL Monroe, Old Dominion, South Alabama, Troy, Southern
Miss). ESPN flags them as conference games; our data has Louisiana Tech in
C-USA. Louisiana Tech is understood to have moved to the Sun Belt for 2026,
which matches ESPN, so our 2026 membership is stale (likely sourced before the
move appeared in ESPN's group member lists). To be confirmed before acting.

Fix: 2026 conference re-source
(`ops/producer/football/ncaa/franchise-seasons/seasonYear/2026/conferences/source`),
then a 2026 fact rebuild. This also corrects Louisiana Tech's 2026
conference record.

## Check 4: 2026 games missing entirely (OPEN, current season)

Found while checking Louisiana Tech's standings (which were correct: 8-2,
6-0 Sun Belt; 2026 conference games use ESPN's flag, so stale membership did
not affect them). 16 of 138 FBS teams have fewer than 11 contests in 2026,
all in the Sun Belt or C-USA:

| Team | Conf | Contests |
|---|---|---|
| Marshall | Sun Belt | 4 (ESPN lists 12) |
| Kennesaw State, Missouri State | C-USA | 5 |
| Georgia Southern, Georgia State, Coastal Carolina, Old Dominion, App State | Sun Belt | 6 |
| Middle Tennessee, Western Kentucky, Delaware, Sam Houston | C-USA | 6 |
| FIU | C-USA | 7 |
| Troy, UL Monroe | Sun Belt | 8 |
| Southern Miss | Sun Belt | 9 |

Marshall detail: ESPN's team schedule has 12 events; 8 have no Contest at
all in our database (no ContestExternalId for 401871045, 401871047,
401869943, 401871096, 401869951, 401869928, 401869958, 401871102). The
missing games are mostly against other teams in the list above, so roughly
40-50 games are absent, including upcoming ones (missing from pick'em
matchups as well as Splits). Weekly 2026 contest totals look normal (210-250
per week), so this is not a whole-week gap.

### Root cause

A season's games are discovered only through each team-season's ESPN event
list, which `TeamSeasonDocumentProcessor` follows only when the TeamSeason is
new or the request names `Event`. The 2026 TeamSeasons were all created on
2026-04-14 (run `f01a5891`, 1,481 contests), when ESPN's lists for these
schools were incomplete (Marshall's had 2 of its 12 games). ESPN now lists
all 12 (`seasons/2026/teams/276/events`, count 12).

After April, the only recurring re-read of team-seasons is a side effect of
the weekly rankings crawl (Provider ResourceIndex
`ab980339-9958-4238-8db1-7459c556b6c7`, `seasons/2026/rankings`, cron
`5 14 * * 0`): it re-sources ranked and vote-receiving teams, whose event
lists add new games. That run created 1,333 of the 2026 contests (Aug 17 to
Nov 15). A game between two teams that never appear in a poll is never
discovered, which is exactly the Sun Belt / C-USA pattern (Marshall at JMU
exists because JMU is polled; Marshall vs Georgia State does not). Same
mechanism as the athlete-season-stats gap (stats sourced only as a poll-crawl
side effect).

`ContestUpdateJob` and enrichment only refresh contests that already exist,
so they cannot close this gap.

### Immediate fix applied (2026-11-18)

Owner had also seen this the previous weekend (Missouri State, MTSU). Option 1
run 2026-11-18 09:04 UTC: per-team
`POST /api/football/ncaa/franchises/{slug}/seasons/2026/source` for the 16
teams (all 202; this route always runs the full TeamSeason cascade). 2026
contests went 2,875 -> 2,922 (+47: 38 already played, 9 upcoming), and every
one of the 16 teams now has 11+ games (Marshall, Missouri State, Kennesaw
State, MTSU: 12). All 38 played games finalized and projected by 09:21 UTC.
The late games left 84 FBS rows with stale WinsBefore/LossesBefore; the 2026
fact rebuild (09:22 UTC) brought that to 0 with 0 failed jobs.

Options 2 and 3: PR #871 adds `DocumentType.Event` (football only) to the
weekly `FranchiseSeasonEnrichmentJob` TeamSeason re-read (Sunday 10:00 UTC),
so every team's schedule is re-read weekly whether or not it is polled. Its
first run after deploy is the full-extent check (option 3): count that run's
discoveries with
`select count(*) from "Contest" where "CreatedBy" = '<CorrelationId>'`, the
CorrelationId being on the job's "Season statistics and schedule refresh
requested" log line.

First run (manual trigger, 2026-11-18 10:33 UTC, CorrelationId
`1323e87f-20f6-41ee-aa04-1ccee6cafe11`, 830 teams): 889 contests discovered,
settled by about 11:30 UTC. 0 involve an FBS team (the per-team fix had
caught them all), 6 involve an FCS team, 883 are D2/D3/NAIA-only (112
upcoming). Kept by decision: capture everything ESPN publishes. No 2026 fact
rebuild needed (facts cover FBS-involved games only).

### Fix options

1. Immediate: re-source the event lists of the affected team-seasons
   (per-team `franchise-seasons/id/{id}/source`, or the whole season via
   `franchises/seasons/2026/source`, with an Event-centred filter and no
   plays), so the missing contests are created before the next slate.
2. Durable: a recurring in-season job that re-reads every current-season
   team-season's event list (Event filter only), independent of the polls.
3. Check all FBS teams against ESPN's per-team event counts to confirm the
   full extent (16 short teams found by a fewer-than-11 threshold; a team
   with 11+ games could still be missing one).

## Incident: deploys silently stuck (resolved)

#870 appeared not to work because no new Producer pod could start since
2026-11-16 12:37 UTC: the client secret of service principal
`sp-sportdeets-k8s-apps` (k8s secret `azure-identity`, used to read Key
Vault and App Configuration at startup) expired after one year. Old pods kept
serving the previous build. Resolved 11-17 by issuing a new secret and
updating `azure-identity`; the crash-looping pods recovered on their own.
Follow-ups: remove the expired credential, find the source of truth for
`azure-identity`, set an expiry reminder, consider workload identity.

## Other open items

- `RebuildSeasonFacts` enqueues every finalized contest (about 3.7k per NCAA
  season) though only FBS-involved games are projected (about 870). An
  FBS-only enqueue would cut the work about 4x.
- The API ops relay times out at 100 s, so a slow season rebuild returns
  HTTP 500 although the Producer finishes the work.
- NFL Producer Hangfire holds 44 stale failed projection jobs from 11-14
  (connection pool exhaustion).
