# War Room Analytics

## Status (2026-11-19)

This section is the only current status. The rest of the doc is the
2026-11-01 design plus dated build notes, and the audits under
`war-room-analytics/` are point-in-time records; neither is updated after
the fact.

Live in prod:

- Phase 0: athlete season stats refresh (#852), coverage audit, score
  roll-up and season floor 2000 (#856), season-accurate conference
  membership (#857), rank sync on update (#853).
- Phase 1: season leader cards (#862).
- Phase 2: `Venue.TimeZone` (#863), `FranchiseContestFact` projection,
  maintenance and per-season rebuild (#864, #865, #866), Splits widget
  (#867, #869, #870). Facts cover NCAA and NFL 2001-2026 (backfill 11-16).
- Weekly enrichment re-reads every football team's schedule (#871).

Open:

- Duplicate NCAA games (11 pairs, 2008/2012/2013) overstate records for
  the 22 affected team-seasons. Needs a dedup design, then a fact rebuild
  and re-enrich of those seasons. See the 11-17 audit, Check 2.
- Historical NFL `GroupSeasonMap` is null before 2026, so NFL Splits waits
  on Phase 5.
- Phases 3-5 not started; MCP hosting undecided.

## Goal

Grow the web War Room from one widget into an analytics surface, and build the
data layer that can answer arbitrarily specific questions, e.g.:

> What is LSU's record against SEC opponents at night, in November, when LSU
> is ranked in the AP Top 10 and the opponent is unranked?

That question is not a widget. It is the yardstick for the data layer: if the
layer can answer it with a simple filter + aggregate, it can feed widgets,
matchup context, and an agent alike.

### Scope

- **Sport:** NCAA football first. NFL is a follow-up; the design must not
  preclude it, but nothing here is built for NFL in the first pass.
- **Seasons:** 2000 to present. Working assumption: data is valid post-2000.
  Phase 0's coverage audit tests that assumption; where it fails, the cutoff
  moves (per data family, see "Data tiers") rather than the layer papering
  over gaps.
- **Out of scope:** general natural-language sports Q&A (see "Positioning").

## Positioning

StatMuse (Disney- and Google-backed, licensed data, dedicated NLP team)
already answers general "ask any sports question" queries. This feature does
not compete with that, and does not need to:

1. **It is a portfolio piece.** The point is demonstrating the whole chain
   owned end to end: ESPN ingestion, canonical model, analytical projection,
   typed query API, agent/MCP surface. Overlap with a funded product is
   irrelevant to that.
2. **The product angle is narrow and contextual, not general.** sportDeets
   knows things StatMuse does not: the user's leagues and picks, the spread at
   kickoff, the AI previews. The analytics layer's first product consumers are
   spread-contextualized history (`docs/features/matchup-spread-context.md`),
   matchup research beside a pick, and War Room widgets — not open-ended Q&A.
3. **The MCP/agent surface is a thin layer over typed tools**, built because
   it demonstrates the architecture, not to win a Q&A market.

## State at design time (2026-11-01)

- `src/UI/sd-ui/src/components/warRoom/WarRoomPage.jsx`: one real widget
  (`FranchiseMetricsGrid`, backed by `GET ui/analytics/franchise-season/{year}`
  → `GetFranchiseSeasonMetricsQueryHandler` → `IFranchiseClientFactory` →
  Producer) plus a static "More Widgets Coming Soon" placeholder.
- `analyticsApi.js` also defines `getContestsByWeek`, which hits a route that
  does not exist on `AnalyticsController`. Remove or implement in Phase 1.
- Stats storage is EAV-shaped (category → stat → value):
  - Season: `AthleteSeasonStatistic` → `AthleteSeasonStatisticCategory` →
    `AthleteSeasonStatisticStat` (`Name`, `Value`, `PerGameValue`, ...).
  - Per game: `AthleteCompetitionStatistic*` (same shape),
    `CompetitionCompetitorStatistic*` (team box score).
  - Fine for storage and ESPN fidelity; poor for ad hoc analytics (every
    filter is a pivot).
- Game context already captured:
  - `ContestBase`: `StartDateUtc`, `SeasonYear`, `Week`, `SeasonPhaseId`,
    `HomeScore`/`AwayScore`, `WinnerFranchiseSeasonId`,
    `SpreadWinnerFranchiseSeasonId`, `OverUnder`, `FinalizedUtc`,
    `CancelledUtc`, `VenueId`.
  - `CompetitionBase`: `IsNeutralSite`, `IsConferenceCompetition`,
    `IsDivisionCompetition`, `TimeValid`, `DateValid`, `Odds`.
  - `CompetitionOdds`: multiple providers (`ProviderPriority`), `Spread`,
    `OverUnder`, open/current/close totals, ATS winner.
  - `FranchiseSeason`: `GroupSeasonMap` (e.g. `NCAAF|NCAA|fbs|sec`), W/L.
  - `FranchiseSeasonRanking`: per poll (`Type`), `Date`, `SeasonWeekId`,
    `Rank.Current`, `Occurrence.Number`.
  - `Venue`: `Latitude`/`Longitude`, `State`. **No time zone.**

## Phase 0 — prerequisites

### 0a. Athlete season statistics refresh (DONE, shipped in #852)

Athlete season stats only refreshed as a side effect of the NCAA poll crawl
(poll teams only; NFL never). Leaders widgets built on that would be stale or
partial.

- `FranchiseSeasonEnrichmentJob.RefreshedChildTypes` widened to
  `TeamSeasonStatistics`, `AthleteSeason`, `TeamSeasonLeaders`,
  `AthleteSeasonStatistics`. The `IncludeLinkedDocumentTypes` filter is honored
  at every hop (TeamSeason → roster index → existing AthleteSeason spawns only
  statistics; TeamSeasonLeaders spawns only leader statistics).
- `EnqueueSingleFranchiseSeasonEnrichmentCommandHandler` (the single-team
  "make current" button) uses the same list, so it stays leg-for-leg with the
  weekly job.
- Schedule moved from `Cron.Weekly` (Sun 00:00 UTC = Saturday evening ET,
  mid-slate) to `0 10 * * 0` (Sun 10:00 UTC): stats then include the Saturday
  slate, and the ~45k-document athlete fan-out no longer competes with live
  games for the ESPN rate budget.
- Verification after deploy: one Sunday cycle, then confirm
  `AthleteSeasonStatistic.ModifiedUtc` advanced for non-poll FBS teams.

### 0b. Coverage audit (2000 to present, NCAA) — DONE 2026-11-01

Results: `war-room-analytics/2026-11-01-coverage-audit.md` (SQL alongside).
Gating findings for Phase 2: `CuratedRankCurrent` never updated after first
ingestion (2026 all unranked); historical scores not rolled up to `Contest`
for several seasons; pre-2011 conference membership wrong for realigned
teams. Original plan, per season, FBS-involved contests:

| Data family | Measure |
|---|---|
| Schedule + scores | contests per season vs expected (~700-850 FBS-involved); finalized with both scores |
| Kickoff time | `% TimeValid` (historical ESPN docs often have date-only kickoffs) |
| Conference flag | `% IsConferenceCompetition` consistent with both teams' `GroupSeasonMap` conference |
| Polls | AP poll present per week (distinguishes "unranked" from "no poll data") |
| Odds | `% with Spread`; `% with ATS result` |
| Team box | `% with CompetitionCompetitorStatistic` |
| Athlete box | `% with AthleteCompetitionStatistic` |
| Plays | `% with plays` (known: FBS 2008-2025 94-99.9%; 2005-2007 86-90%; 2001-2004 60-66%; 2002 ~4%; 2000 0%) |

The audit output sets the per-family floor (see "Data tiers").

## Phase 1 — Leaders widgets (no new data layer)

**Status: shipped in #862 (built 2026-11-10 on `feat/war-room-leaders`).**
As built:

- Producer `GET api/athletes/leaders?seasonYear&category&stat&perGame&limit`
  (`GetAthleteSeasonLeadersQueryHandler`); API `GET ui/analytics/leaders/{seasonYear}`
  via `IAthleteClientFactory`; web `LeadersPanel` + `LeaderCard` replace the
  placeholder.
- Newest stat doc per athlete-season wins: most 2026 NCAAFB athletes carry two
  identical docs (`SplitType` "" and "season"), so a naive board lists every
  player twice.
- Per-game = total / `general.gamesPlayed` (ESPN's per-game values are sparse);
  qualification is the NCAA rule, 75% of the team's finalized non-preseason
  games. No per-stat definitions table yet: the first six are counting stats.
- No headshots and generated team marks only (licensing); the card falls back
  to the color roundel.
- Season selector 2004-present (FBS athlete season stats start 2004; 2025 is
  thin, about 4.9k athletes vs about 8.4k in neighboring years).
- Prod check (read-only): Phase 0a verified, the 2026-11-08 Sunday run
  refreshed 28,378 athlete stat rows (14,660 FBS). Main leaders query
  130 ms on prod data.

Current-season leaders come straight from ESPN's season totals
(`AthleteSeasonStatistic*`), which Phase 0a keeps current.

### API

One generic endpoint, not one per widget:

```
GET ui/analytics/leaders/{seasonYear}
    ?sport=football&league=ncaa
    &scope=athlete|team
    &category=passing
    &stat=passingYards
    &perGame=false
    &limit=10
```

- API handler → Producer client (API never reads Producer's DB) → Producer
  query over the EAV tables: filter `Category.Name`/`Stat.Name`, order by
  `Value` (or `PerGameValue`), `.AsNoTracking()`, project to DTO.
- Returns rank, athlete (name, slug, headshot), franchise (slug, logo,
  colors), value, display value.
- **Qualification thresholds** are required for rate stats (a 3-for-3 backup
  leads completion %). Define per stat in a Producer-side table of leader
  definitions (min attempts / carries / targets per team game), not in the UI.
- `scope=team` reads team season stats / `FranchiseSeasonMetric`, which covers
  the placeholder's YPC, red zone, third down, turnovers.
- New route ⇒ route snapshot test update (`ROUTE_SNAPSHOT_UPDATE=1`).

### Web

- A widget is config: `{ title, scope, category, stat, perGame, sort }`.
- Initial set (~6): Passing Yards, Rushing Yards, Receiving Yards,
  Interceptions (defense), Sacks, Tackles for Loss. Exact ESPN category/stat
  names confirmed from data, not assumed.
- Replace the placeholder. Desktop-first layout (War Room is web).

## Phase 2 — `FranchiseContestFact` (team-game projection)

One row per team per game: the denormalized shape that makes the LSU question
a `WHERE` + `GROUP BY`.

### Columns (first cut)

| Group | Columns |
|---|---|
| Identity | `ContestId`, `FranchiseSeasonId`, `FranchiseId`, `OpponentFranchiseSeasonId`, `OpponentFranchiseId`, `SeasonYear`, `Week`, `SeasonPhase` |
| Time | `KickoffUtc`, `KickoffLocal`, `KickoffTimeKnown` (from `TimeValid`), `KickoffWindow` (bucket), `LocalMonth`, `LocalDayOfWeek` |
| Site | `HomeAway` (home/away/neutral), `VenueId` |
| Grouping | `ConferenceSlug`, `OpponentConferenceSlug`, `IsConferenceGame`, `Division` (fbs/fcs), `OpponentDivision` |
| Rank going in | `RankGoingIn`, `OpponentRankGoingIn` (from `CuratedRankCurrent`; see below) |
| Record going in | `WinsBefore`, `LossesBefore`, `OpponentWinsBefore`, `OpponentLossesBefore` |
| Result | `PointsFor`, `PointsAgainst`, `Margin`, `Result` (W/L/T) |
| Market (odds tier) | `Spread` (from this team's perspective), `OverUnder`, `AtsResult`, `TotalResult` |
| Box (pivoted) | ~20 key team stats as columns: total yards, pass/rush yards, YPP, turnovers, 3rd-down %, red-zone %, penalties, TOP. Exact list from the team box categories. |

### Derivation rules (the hard parts)

- **Local time.** `Venue` has lat/long but no time zone. Add
  `Venue.TimeZone` (IANA), populated once from coordinates (extend
  `VenueGeoCodeJob`). Rows with no venue or no zone get null local fields, not
  a guess. Whether to take a lat/long → zone library or a hand-maintained
  state map (states split across zones: IN, KY, TN, FL panhandle, ...) is a
  Phase 2 decision; no dependency added until then.
- **Month uses the local date.** An 8pm ET kickoff on Nov 30 is Dec 1 UTC.
  "In November" must mean local November.
- **Night.** Proposed: `KickoffWindow` buckets on local kickoff — Noon
  (< 14:00), Afternoon (14:00-17:59), Night (>= 18:00). `KickoffTimeKnown =
  false` ⇒ window null; such games never count as night or day.
- **Rank going in** = `CompetitionCompetitor.CuratedRankCurrent` (ESPN's rank
  on the game's competitor; 99 = unranked). The audit showed weekly poll
  tables only exist for 2025+, so poll-date lookups cannot cover history.
  The update-path bug that froze 2026 ranks at 99 is fixed (#853). Open: whether
  curatedRank switches from AP to CFP once CFP rankings publish; until
  verified, name the column `RankGoingIn`, not `ApRank`.
- **Unranked vs unknown.** 99 = unranked; null (e.g. all of 2000) = unknown,
  and filters on "unranked" exclude unknown rows.
- **Conference game.** Prefer `IsConferenceCompetition` from ESPN; the audit
  checks it against `GroupSeasonMap` conference equality. Non-conference
  games between conference mates (rare, scheduled OOC) are ESPN's call.
- **Record going in** = W/L from finalized contests before kickoff, same
  season (same computation as record enrichment, as of a point in time).
- **Spread perspective.** Stored from the row's team perspective (negative =
  favored), from the top-priority provider, consistent with what the existing
  spread-context SQL uses.

### Storage and maintenance

Recommendation: **EF-managed tables, maintained by a projector**, in each
sport's Producer DB.

- Upsert both rows of a contest on `ContestFinalized` (and on re-enrichment /
  odds-late paths that change result or ATS). Idempotent by
  `(ContestId, FranchiseSeasonId)`.
- A rebuild job (per season) for backfill 2000 to present and for re-projecting
  after a derivation change. Version the projection (`ProjectionVersion`
  column) so a rebuild can be verified.
- Rank/record columns depend on *other* data (polls, prior games). When a poll
  lands late or a prior game is corrected, the projector must recompute
  affected rows; the rebuild job is the backstop.

Alternative considered: Postgres materialized views. Simpler to stand up and
trivially cheap to refresh at this size (~4k team-game rows per season, ~100k
total for 2000+). Rejected as the primary because the derivations above
(local time, poll-date rank, unranked-vs-unknown) are logic that should be unit
tested in C#, and the codebase prefers LINQ/EF over large raw SQL. Revisit if
the projector proves heavier than expected.

Naming: `FranchiseContestFact` follows the existing vocabulary (Contest =
game, FranchiseSeason = team-season). Open for a better name before the
migration; table name singular.

### Query API

- Producer: one typed query endpoint over the fact table with a filter object
  (team, opponent, season range, conference/opponent conference, conference
  game, home/away/neutral, kickoff window, month, rank ranges for both sides,
  spread range, ...) returning an aggregate (W-L-T, ATS, avg margin) plus the
  matching game list.
- API: proxy via the client factory, slug-based route.
- First consumers: a War Room "Splits" widget (team + a few filter chips), and
  optionally the existing spread-context queries (`GetFranchiseMarginFact`,
  `GetFranchiseAtsBucket`) could move onto the fact table later. Not required.

## Phase 3 — `AthleteContestFact` (athlete-game projection)

One row per athlete per game with pivoted per-game stats (pass att/comp/yds/
TD/INT, rush att/yds/TD, rec/tgt/yds/TD, tackles, sacks, INTs, ...) plus the
same game context columns as Phase 2 (by join or copy; decide once Phase 2
query patterns are known).

- Enables historical leaders ("most 300-yard games vs ranked opponents since
  2010") and per-game splits. Phase 1 leaders stay on ESPN season totals.
- Size is the concern: tens of athletes per team-game across 25 seasons ⇒
  millions of rows. Index for the common filters; measure before adding more.
- Floor set by the athlete-box audit in 0b, likely later than 2000.

## Phase 4 — Agent / MCP surface

- Typed tools over the Phase 2/3 query endpoints, e.g.
  `query_team_games(filters) → aggregate + games`,
  `query_leaders(scope, category, stat, season, filters)`,
  `resolve_team(name) → slug`.
- **No raw SQL tool initially.** It would also conflict with "API never reads
  Producer's DB". A read-only role over the fact tables can be reconsidered
  later as an explicit exception.
- Hosting decision deferred: MCP endpoint in API (calls Producer clients,
  Firebase auth) vs a small dedicated service.
- Later: a War Room "Ask" box that calls the same tools via the model
  routing already in place (OpenRouter). LLM cost on a free product ⇒ rate
  limit or gate.
- Prompts never in source control (public repo).

## Phase 5 — NFL

Same projections and endpoints under the NFL Producer. NFL-specific concerns:
no AP poll (rank columns null), divisions matter more than conferences,
historical NFL rosters are fabricated by ESPN (affects athlete facts, see
`athlete-season-fabrication-remediation.md`).

## Data tiers

Answers must degrade honestly by data family, never imply completeness:

Measured 2026-11-01 (`war-room-analytics/2026-11-01-coverage-audit.md`):

| Tier | Needs | Floor | Notes |
|---|---|---|---|
| Results | scores, home/away, record going in | 2000 after score roll-up | 2010/2016/2018 holes |
| Conference | membership, conference-game flag | 2001 (season-accurate map since #857), 2014 (ESPN flag; derived from membership before) | 2000 membership unverified |
| Time-of-day | kickoff time + venue zone | 2001 | 2000 date-only |
| Rankings | rank at game time | 2001-2025 via `CuratedRankCurrent` | 2026 frozen-rank bug fixed in #853; weekly poll tables 2025+ only |
| Market | spread / ATS results | 2012 | ATS follows result gaps |
| Team box | team per-game stats | 2004 | 2011, 2016, 2018 thin |
| Athlete box | athlete per-game stats | 2004 | 2018 28%, 2025 42% |

A query mixing tiers (e.g. night + ranked) is limited to the newest floor among
the tiers it uses, and the response states the effective range.

## Open decisions

Decided 2026-11-12 (owner):

1. Fact table names: `FranchiseContestFact` (and `AthleteContestFact`).
2. Kickoff windows: Noon < 14:00, Afternoon 14:00-17:59, Night >= 18:00 local.
3. Venue time zone source: offline lookup library (GeoTimeZone 6.1.0, MIT,
   embedded Timezone Boundary Builder data; 7.0.0 was nine days old, so not
   taken yet). Prod check: all 919 venues have coordinates; FBS games carry a
   venue 87-95% for 2001-2013, ~100% from 2014. Games without a venue get null
   local fields.
4. Projection scope: FBS-involved games only.

Still open:

5. MCP hosting (API vs dedicated service).

## Phase 2 slices

1. `Venue.TimeZone` (IANA) from coordinates: set when a venue is geocoded;
   the weekly `VenueGeoCodeJob` fills any geocoded venue without one (this
   also backfills existing venues).
   DONE: #863 merged and deployed 2026-11-12; job run for NCAA, NFL, MLB. All
   geocoded venues have a zone. One boundary miss: El Paso venues (Sun Bowl,
   Southwest University Park, ~2 km from the border) resolved to
   `America/Ciudad_Juarez`; corrected by hand to `America/Denver` in all
   three prod DBs (NCAA by State, NFL by Id since that row has no address,
   MLB by State). The job never overwrites a non-null zone, so the fix holds.
2. `FranchiseContestFact` entity, migration, projector (pure derivation, unit
   tested).
   Built on `feat/franchise-contest-fact` (2026-11-12):
   - Football-only table on `FootballDataContext` (rank and box columns are
     football); migration `12NovV2_FranchiseContestFact`.
   - `FranchiseContestFactProjector` (pure) + `ProjectContestFactsCommandHandler`
     (load, project, idempotent upsert; removes rows for a contest that is no
     longer finalized / is cancelled / out of scope). Preseason excluded.
   - Spread from `OddsProviderPreference.SelectPrimary` flipped to the team's
     side; ATS from `Contest.SpreadWinnerFranchiseSeasonId`, push only when
     line + margin = 0. Record going in and conference game use the record
     enrichment's rules. Date-only kickoffs keep ESPN's date, no window.
   - Box: 21 count columns from ESPN team statistics (names confirmed on prod).
   - Manual trigger: `POST api/franchise-contest-facts/contests/{contestId}`.
   DONE: #864 merged and deployed; ops proxy allowlist #865. Smoke test on
   prod 2026-11-14 (Miami at Notre Dame, Bills at Vikings) matched source
   data on every column checked.
3. Maintenance: upsert on `ContestFinalized` / re-enrichment; per-season
   rebuild endpoint for backfill.
   Shipped in #866 (built on `feat/franchise-contest-facts-maintenance`):
   - `ContestFinalizedHandler` (football pods) enqueues the projection; this
     covers finalization, re-enrichment (re-finalizes) and score corrections
     (re-enrich, #861). The odds-late path does not republish
     ContestFinalized, so `FootballContestEnrichmentProcessor` enqueues the
     projection there itself.
   - `POST api/franchise-contest-facts/seasons/{seasonYear}` enqueues one
     projection per finalized contest plus every contest that already has
     rows. Also the backstop for record-going-in drift: a correction to an
     earlier game is not followed to later games.
   - ProjectionVersion 2: NFL GroupSeasonMap parses as conference then
     division ("NFC", "NFC NORTH"); rank stored for NCAAFB only. Prod NFL
     FranchiseSeason.GroupSeasonMap is null for 1990-2025 (only 2026 set),
     so historical NFL rows have no conference/division: a Phase 5 item.
4. Producer query + API proxy + War Room Splits widget.
   Decided 2026-11-15 (owner): summary + game list; filter chips Site,
   Opponent rank, Favorite/underdog, Kickoff window, Conference game; NCAAFB
   only in v1 (NFL waits for the Phase 5 GroupSeasonMap fix).

   **UI (top-down).** A `SplitsPanel` on the War Room page:
   - Controls: team picker (FBS teams from the existing
     `/ui/analytics/franchise-season/{year}` list, no new endpoint), season
     from/to selects, and chip groups (single-select each, "Any" default):
     Site (Home / Away / Neutral), Opponent (Ranked / Top 10 / Unranked),
     Line (Favorite / Underdog), Kickoff (Noon / Afternoon / Night),
     Conference (Conference / Non-conference).
   - Summary: W-L-T and pct, ATS W-L-P (games with a line), avg margin, avg
     PF / PA, favorite W-L and underdog W-L.
   - Range note: the effective season range when a chip raised the floor
     ("Opponent rank data starts 2001; showing 2001-2025").
   - Game list, newest first: season, week, site marker, opponent (with rank
     going in), result and score, team's line, ATS result.

   **Wire contract.** `FranchiseSplitsDto` (Core):
   `FranchiseSlug, FranchiseName, SeasonFrom (effective), SeasonTo,
   RequestedSeasonFrom, Summary, Games[]`. Summary: `Games, Wins, Losses,
   Ties, AtsWins, AtsLosses, AtsPushes, AvgMargin, AvgPointsFor,
   AvgPointsAgainst, FavoriteWins, FavoriteLosses, UnderdogWins,
   UnderdogLosses`. Game: `ContestId, SeasonYear, Week, SeasonPhaseTypeCode,
   KickoffUtc, Site, KickoffWindow, IsConferenceGame, RankGoingIn,
   OpponentSlug, OpponentName, OpponentRankGoingIn, PointsFor,
   PointsAgainst, Result, Spread, AtsResult`. Enums travel as strings.

   **Filter rules.**
   - Opponent rank: Ranked = 1-25, Top 10 = 1-10, Unranked = 99. Null rank
     (unknown) matches none of them.
   - Line: Favorite = spread < 0, Underdog = spread > 0. Pick'em (0) and no
     line match neither. Summary favorite/underdog splits use the same rule.
   - Kickoff window: null window (date-only kickoff) matches no window chip.
   - Effective floor per chip (data tiers above): rank 2001, line 2012,
     kickoff 2001, conference game 2001 (lowered from 2014 once #857 made
     membership season-accurate). Results-only floor 2001 (2000 is
     date-only and unverified for membership).

   **Routes.** Producer `GET api/franchise-contest-facts/franchises/{slug}/splits`
   on `FranchiseContestFactsController` (football-only mode guard, same as
   the commands); query params `seasonFrom, seasonTo, site, opponentRank,
   line, kickoffWindow, conferenceGame`. API `GET /ui/analytics/splits/{slug}`
   (+ `sport`, `league`, same params) relaying through `FranchiseClient`.
   Aggregation runs in C# over the filtered rows (at most ~350 rows per team
   for 25 seasons), so the rules above are unit tested, not SQL.

   Shipped in #867 (built 2026-11-15 on `feat/war-room-splits`): Producer query + 8 handler tests, `FranchiseClient.GetFranchiseSplits`,
   API relay + route snapshot, `SplitsPanel` + 6 Vitest tests. The War Room
   page is now tabbed (Splits / Season Leaders / Franchise Season Metrics,
   `?tab=` in the URL, panels mount on first visit). The 2001-2025
   fact backfill ran 2026-11-16 (NCAA and NFL), so prod covers 2001-2026.
