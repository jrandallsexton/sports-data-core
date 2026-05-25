# Team Schedule Endpoint (Mini-Schedule v2)

Slim, snapshot-capable schedule endpoint that backs the embedded
MiniSchedule on the PicksPage MatchupCards (web + mobile). Builds on
the mini-schedule rework shipped in PR #369.

---

## Motivation

When the MiniSchedule chevron is tapped today, the UI calls the full
TeamCard endpoint
(`/ui/teamcard/sport/{sport}/league/{league}/team/{slug}/{year}`)
and discards everything except `Schedule[]`. Two problems surfaced
after PR #369 flipped the sort to newest-first:

1. **Wrong slice for in-season teams.** The component shows the
   trailing 10 games of the *season array*, not the trailing 10
   *completed* games. Mid-season this means future, unplayed games
   appear at the top.
2. **No historical snapshot.** When reviewing a prior week's picks
   ("Week 4"), the mini-schedule will show results from Weeks 5+
   that the picker could not have known about. That leaks future
   information into a historical context view.

Fixing both inside the existing handler would re-shape its contract
for every caller. A new, purpose-built endpoint is cheaper and
keeps the legacy TeamCard endpoint (powering the full team page)
untouched.

---

## Why a numeric week filter doesn't work

The first cut of this endpoint took `?week=N` and applied
`sw."Number" < @Week`. Live testing surfaced two failures that
forced a redesign:

1. **MLB drops same-week completed games.** A `SeasonWeek` for MLB
   spans ~7 days. The picker's "Week 9" matchups include games
   played within Week 9 — but `sw.Number < 9` strips any game
   tagged with `SeasonWeek.Number = 9`, including ones that
   finished before the picks locked. The picker sees a hole in
   their team's recent form.
2. **Football post-season smuggles in low-numbered weeks.** ESPN
   reuses Week 1, 2, 3 numbering for the post-season but on a
   different `SeasonPhase`. `sw.Number < 9` happily matches
   post-season Week 1/2/3 games that happen *months later* than the
   regular-season Week 9 cutoff the picker had in mind.

Both modes share a root cause: `SeasonWeek.Number` is a coarse,
sport-shaped bucket, not a temporal boundary. A date cutoff fixes
both — MLB same-week games come back in (their `FinalizedUtc` is
≤ Week 9's `EndDate`); football post-season is naturally excluded
(its `FinalizedUtc` is after regular-season Week 9's `EndDate`).

The endpoint now takes `?asOfDate=<ISO>` instead. The API derives
`AsOfDate` from the displayed week's `SeasonWeek.EndDate`, surfaces
it on `LeagueWeekMatchupsDto.AsOfDate`, and the UI threads it into
the MiniSchedule fetch.

---

## Goals

- Return **completed games only** (`FinalizedUtc IS NOT NULL`).
- Accept an optional **`asOfDate`** cutoff parameter; with
  `asOfDate=<ISO>` return games with `FinalizedUtc <= @AsOfDate`
  (inclusive — see *Cutoff semantics*).
- Slim payload: schedule array only, no team header / record / etc.
- Same shape as today's `TeamCardScheduleItemDto[]` so the existing
  web + mobile MiniSchedule components don't need DTO changes.
- Slug-routed Producer endpoint matching the
  `/api/franchises/{slug}/seasons/{year}/...` convention from
  `CLAUDE.md`.

## Non-goals

- Not replacing the full TeamCard endpoint. The team page still
  needs the full payload (logo, record, ranking, etc.).
- Not changing the Season Overview developer tool.
- Not introducing a new DTO shape for the schedule items.

---

## API surface

### API (`SportsData.Api`)

```http
GET /ui/teamcard/sport/{sport}/league/{league}/team/{slug}/{year}/schedule
    ?asOfDate={ISO 8601, optional}
```

- Returns `TeamCardScheduleItemDto[]` directly (no envelope).
- `asOfDate` is optional. Omitted → all completed games for the
  season.
- 200 with `[]` when the team exists but has no completed games
  yet (or no games at or before the cutoff date).
- Handler wraps the typed FranchiseClient call in the standard
  `Result<T>` pattern; controller uses `[FromServices]` injection.

### Producer (`SportsData.Producer`)

```http
GET /api/franchises/{slug}/seasons/{year}/schedule
    ?asOfDate={ISO 8601, optional}
```

- New action on `FranchisesController` (slug-routed, per
  `CLAUDE.md` read-endpoint convention).
- Dapper, projects to `TeamCardScheduleItemDto`, `.AsNoTracking()`
  semantics (Dapper is read-only by default).

### SQL

New embedded resource `GetTeamScheduleCompleted.sql`. Same JOINs as
the existing `GetTeamCardSchedule.sql` but with:

```sql
WHERE (fAway."Slug" = @Slug OR fHome."Slug" = @Slug)
  AND C."SeasonYear" = @SeasonYear
  AND C."FinalizedUtc" IS NOT NULL
  AND (@AsOfDate IS NULL OR C."FinalizedUtc" <= @AsOfDate)
ORDER BY C."StartDateUtc" DESC
```

Sort is `DESC` so the endpoint contract is "newest-first" and the
client doesn't need a `.reverse()` step.

---

## Where AsOfDate comes from

`LeagueWeekMatchupsDto.AsOfDate` is populated server-side, sourced
from the SeasonWeek the matchup's contests belong to:

```text
LeagueWeekMatchupsDto.AsOfDate
  = first canonical matchup's contest
  → Contest.SeasonWeekId (FK)
  → SeasonWeek.EndDate
```

All contests in a single league-week share the same SeasonWeek, so
the first matchup's value is authoritative. The matchups
enrichment SQL (`GetMatchupsByContestIds.sql`) was extended to
JOIN `SeasonWeek` on the contest and project `EndDate` as
`SeasonWeekEndDate`. The Producer-side `LeagueMatchupDto` carries
that field; the API handler picks it off the first row.

When a week has no canonical matchups (empty week, missing data),
`AsOfDate` is null and the MiniSchedule fetch falls back to "no
cutoff" — but in that scenario there are no matchup cards rendered
either, so the mini-schedule isn't reachable.

---

## Cutoff semantics

`?asOfDate=<ISO>` filter is **inclusive** (`FinalizedUtc <= @AsOfDate`).

Rationale: `AsOfDate` equals the displayed week's `SeasonWeek.EndDate`.
A picker viewing Week N picks should see games that finished through
the end of Week N — including any that finished within Week N itself
before the picks locked. Exclusive would strip those.

**MLB:** Week 9's `EndDate` is the calendar boundary of Week 9.
Games tagged with `SeasonWeek.Number = 9` that finalized at or
before that date are included (the bug the prior week-number filter
caused). Games from Week 10+ are excluded by date even if their
SeasonWeek hasn't been authored yet.

**Football post-season:** Post-season SeasonWeek rows exist on a
different SeasonPhase with `Number = 1, 2, 3` but `StartDate`/
`EndDate` weeks or months after regular-season Week 9. The date
filter excludes them naturally.

---

## Frontend plumbing

### Where AsOfDate comes from in the UI

The PicksPage already fetches
`/ui/leagues/{leagueId}/matchups/{week}` and renders a MatchupCard
per matchup. `LeagueWeekMatchupsDto` now carries `AsOfDate`. That
value threads down:

```text
PicksPage  →  MatchupList(leagueAsOfDate)
           →  MatchupCard(leagueAsOfDate)
           →  TeamRow(asOfDate)
           →  MiniSchedule(asOfDate)
           →  useTeamSchedule(asOfDate)
           →  GET …/schedule?asOfDate={iso}
```

Outside a pick'em context (team page, future use cases),
`asOfDate` is omitted and the endpoint returns all completed games
for the season.

### Hook shape

Mobile: a lightweight `useTeamSchedule` hook (TanStack Query,
`enabled` gate, key includes asOfDate) replaces the full
`useTeamCard` for the chevron-gated fetch. The full `useTeamCard`
keeps powering the team page itself.

Web: the existing `useTeamSchedule` hook is repurposed to hit the
new endpoint instead of stripping `schedule` out of the full
TeamCard response.

### Components

`MiniSchedule.jsx` / `MiniSchedule.tsx`: the local sort/reverse is
removed (endpoint returns newest-first). The `sport === 'football'
? all : top 10` slice rule stays in the component for now — the
endpoint returns everything completed-and-eligible and the
component trims.

---

## Rollout

1. Producer: SQL + handler + controller action.
2. Producer: matchups enrichment SQL extended to expose
   `SeasonWeekEndDate`; `LeagueMatchupDto` field added.
3. Core: `FranchiseClient.GetTeamSchedule(slug, year, asOfDate?)`.
4. API: query handler + controller action (`?asOfDate=<iso>`).
5. API: `LeagueWeekMatchupsDto.AsOfDate` populated in the matchups
   handler from the first canonical matchup's `SeasonWeekEndDate`.
6. Web: swap MiniSchedule fetch, thread `leagueAsOfDate` down.
7. Mobile: same swap; new typed `useTeamSchedule` hook.
8. Unit test: SQL provider wiring check (resource loads + key
   filter shape present).

Backward compatibility: the legacy TeamCard endpoint is untouched.
No data migration. No event/topic changes. No infra changes.

---

## Risks & mitigations

- **MLB week-boundary drift.** SeasonWeek rows are sourced from
  ESPN; if a week's `EndDate` is set to midnight UTC of the
  following Monday but a Sunday-night game finalizes at 03:15 UTC
  Monday, that game gets included in Week N — which is probably
  desired (the picker effectively had access to that result before
  the next batch of picks). If we ever see misalignment, the fix
  is in the SeasonWeek source, not this endpoint.
- **Empty result for early-season teams.** A team with zero
  completed games returns `[]`; MiniSchedule already renders
  "No recent games." No new handling needed.
- **Cross-week matchup curation.** If a future league type curates
  picks across SeasonWeek boundaries (e.g. a "championship week"
  matchup spanning week 14 + 15), the first matchup's
  `SeasonWeekEndDate` may understate the snapshot. Out of scope
  until such a league ships.
