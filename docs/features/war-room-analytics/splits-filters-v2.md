# Splits filters v2

Status: built on `feat/splits-filters-v2` (2026-11-19), not yet merged. Month
uses option (a) below. The same branch fixes the always-null `Week` on fact
rows (see [Week fix](#week-fix-projectionversion-3)), which needs a
per-season rebuild after deploy.

Next filters for War Room Splits (`SplitsPanel`, Producer
`GetFranchiseSplitsQueryHandler`). The v1 design is Phase 2 slice 4 in
[war-room-analytics.md](../war-room-analytics.md). This note specifies the
next five filters in full and records the rest of the backlog so ideas are
not lost.

All five read columns `FranchiseContestFact` already stores. None needs a
migration, a projector change or a rebuild.

## Data checked (prod NCAAFB, 2026-11-19)

- 41,604 fact rows.
- `OpponentConference` holds the conference slug from `GroupSeasonMap`
  (`sec`, `big10`, `midam`, `belt`, `American`, `UAC`, ...): 34 distinct
  values plus 5 blank rows. The casing is inconsistent and these are slugs,
  not display names.
- The value is the opponent's conference **in that season**, so Texas A&M
  before 2012 is `big12`.
- `SeasonPhaseTypeCode`:

  | Code | Meaning | Rows |
  |---|---|---|
  | 2 | Regular season | 39,826 |
  | 3 | Postseason | 1,770 |
  | 4 | All-star / exhibition | 4 |
  | 5 | All-star / exhibition | 4 |

  The 2012 SEC Championship is phase 2 with a December `LocalMonth`, so
  conference championship games are regular season. Bowls and the playoff
  are phase 3.
- `LocalMonth` is mostly September to November. August has 1,490 rows,
  December 1,914, January 350, and February/March 4. It is null on 1,666
  rows (4%), exactly the rows without a venue. For postseason games that is
  306 of 1,770 rows (17%).
- `Spread` is on almost every row from 2012: 300 of 25,570 rows have none.
  Before 2012 there are only 2 spreads, so 2012 stays the line floor.
  Absolute spreads are spread evenly from 0 to 30.
- Distinct opponents per franchise, all seasons: median 44, max 93.

## The five filters

| Filter | Query param | Values | Matches | Floor |
|---|---|---|---|---|
| Opponent conference | `opponentConference` | conference slug | `OpponentConference == value` | 2001 |
| Opponent | `opponent` | franchise slug | `OpponentFranchiseId == id of slug` | 2001 |
| Season type | `seasonType` | `Regular`, `Postseason` | phase 2 or phase 3 | 2001 |
| Month | `month` | 1-12 | `LocalMonth == value` | 2001 |
| Spread size | `spreadSize` | `Close`, `Moderate`, `Large`, `Huge` | abs(`Spread`) in band | 2012 |

Each is single-select with Any as the default, like v1, and each adds its
floor to `EffectiveSeasonFrom`. All of them combine with the v1 chips by AND.

### Opponent conference

- The match is on the opponent's conference at the time of the game.
  "LSU vs SEC" therefore includes Texas A&M from 2012 only, and "vs Big 12"
  includes Texas A&M before 2012. Say this in the UI tooltip. Filtering by
  the opponent's *current* conference is a different question and is out of
  scope.
- Values are slugs. The response carries display labels (see Options), so
  the UI never shows `midam` or `belt`.
- Blank `OpponentConference` (5 rows) matches no conference.
- This filter is independent of the existing Conference / Non-conf chip.
  Combining them can only narrow the result, so contradictory combinations
  (Conference game with a different opponent conference) return zero games,
  which is accurate.

### Opponent (head-to-head)

- Match on `OpponentFranchiseId`, not `OpponentFranchiseSeasonId`, so the
  series spans seasons.
- The picker lists only teams this franchise actually played in the season
  range (see Options), not all FBS teams. FCS opponents appear too, because
  facts include FBS-vs-FCS games.
- An unknown slug returns zero games, not an error. A typo in a shared URL
  should not break the page.

### Season type

- `Regular` = phase 2, `Postseason` = phase 3.
- Conference championship games are phase 2 in ESPN's data, so they count
  as Regular. The tooltip says so.
- Phases 4 and 5 (8 all-star rows) match neither and only appear under Any.
- Bowl vs playoff is not separable from the stored columns; see the backlog.

### Month

- Chips: Aug, Sep, Oct, Nov, Dec, Jan. February and March (4 rows) appear
  only under Any.
- Uses `LocalMonth`, per the design rule that "in November" means local
  November.
- **Decision needed:** 4% of rows (17% of postseason) have no venue and so
  no `LocalMonth`. As in v1's kickoff window, they match no month. That
  undercounts bowls under Dec/Jan.
  - Option (a): accept it for v1 and state it in the range note.
  - Option (b): have the projector fall back to the Eastern Time date when
    there is no venue. Every FBS game is in the US, so only late Hawaii and
    Pacific kickoffs could land on the wrong day. This costs a
    ProjectionVersion bump and a per-season rebuild.

  Recommendation: (a) now, (b) folded into the next projector change.

### Spread size

- Bands on the absolute spread, from the team's side:

  | Band | abs(spread) |
  |---|---|
  | Close | <= 3 (includes pick'em 0) |
  | Moderate | > 3 and < 7 (3.5 - 6.5 in half points) |
  | Large | >= 7 and < 14 (7 - 13.5) |
  | Huge | >= 14 |

- Size, not direction: Line (Favorite / Underdog) already gives direction,
  and the two combine. "Favorite + Huge" = favored by 14+. Keeping them
  separate avoids an 8-chip group.
- No line (null spread) matches no band.
- The summary's favorite/underdog splits are unchanged.

## Options in the response

The opponent and conference pickers need lists that depend on the team and
season range. Rather than add a route, the splits response gains:

```
Options: {
  Opponents:   [{ Slug, Name, Games }],   // sorted by Name
  Conferences: [{ Slug, Label, Games }]   // sorted by Label
}
```

- Computed over the team's rows for the **requested** season range (raised
  only to 2001), without the chip filters or their floors, so a picker never
  empties itself when a chip changes. This is one extra
  grouped query on a few hundred rows.
- Conference `Label`: the `GroupSeason.ShortName` of the most recent season
  whose `Abbreviation` matches (the map stores `Abbreviation`), falling back
  to the stored value. Prod: `midam` is "MAC", `belt` "Sun Belt", `usa` "CUSA".

## Contract changes

- **Producer `GetFranchiseSplitsQuery`**: add `OpponentConference`
  (string?), `Opponent` (string?), `SeasonType` (enum?), `Month` (int?) and
  `SpreadSize` (enum?).
- **Validator**:
  - Slugs: at most 100 characters.
  - `Month`: 1-12.
  - Enums: `IsInEnum`.
- **`FranchiseSplitsRequest`** (Core) and the API relay: pass the five
  params through as strings or ints. The Producer stays the only validator,
  as in v1.
- **`FranchiseSplitsDto`**: add `Options`. The existing fields are
  unchanged.
- **`analyticsApi.getFranchiseSplits`**: accept and forward the five params.
- The API route snapshot does not change, because the route is the same and
  only query params are new.

## UI

With 13 filters, one wrapping chip row made the groups hard to tell apart
(group labels read like pills, related filters were scattered). The filters
are laid out in three blocks by the question they answer, each with a fixed
label column so chips align:

| Block | Rows |
|---|---|
| Game | Site, Season (Regular / Postseason), Month, Kickoff |
| Opponent | Team (select), Conference (select), Rank, Matchup (In-conference / Non-conference) |
| Betting | Line (Favorite / Underdog), Spread (size bands) |

- **Opponent team and conference** are `<select>` controls, since 30 to 90
  values do not fit as chips. Both are filled from `Options`, with "Any"
  first, and are disabled until a team is picked.
- **The header row** holds team and season range, plus "N filters on ·
  Clear all" when anything is set.
- **Changing team** resets the opponent and conference selections.
- **Range note**: a Spread size selection raises the floor to 2012, the same
  way Line does. When Month is set, the note also says that games without a
  venue are excluded (option a).
- **Desktop first**: the three blocks sit side by side and stack below
  1100px. No phone-specific layout.

## Week fix (ProjectionVersion 3)

`Week` was null on every fact row in prod (all seasons). The projector copied
`Contest.Week`, which the schedule import never populates, while
`SeasonWeekId` is set on every contest. The projector now takes
`SeasonWeek.Number` (falling back to `Contest.Week`), the same rule as
`GetAthleteMatchupSummaries`, and `CurrentProjectionVersion` is 3.

After deploy, run `POST api/franchise-contest-facts/seasons/{year}` for
2001 through the current season, then check no NCAAFB rows remain below
version 3. Verified locally on a 2026-11-19 prod restore.

## Tests

- **Producer handler**:
  - One test per filter: match, non-match, and null-column rows.
  - Spread band edges (3, 3.5, 6.5, 7, 13.5, 14).
  - Phases 4 and 5 excluded from both season types.
  - Floors in `EffectiveSeasonFrom`.
  - `Options` ignores chip filters but honors the season range.
- **Validator**: month bounds, enum range, slug length.
- **Vitest**:
  - The new chips send the right params.
  - The selects are populated from `Options`.
  - Changing team resets opponent and conference.

## Client-side filtering (considered, deferred)

Every chip change is an API call today. The API keeps server-side filtering
regardless, because future widgets and the Phase 4 agent surface will need
it. Whether the panel also filters in the browser is optional. Owner decision
2026-11-19: leave it as is for now.

If it comes back, the options are:

1. Cache responses by parameter set. Chips already visited become instant,
   and the rules stay in one place.
2. Fetch a team's full range once and filter and summarize in the browser.
   Every chip becomes instant, but the rules and season floors are repeated
   in JavaScript and need their own tests.

Option 2 also needs every filterable column on each game in the response.

## Backlog (captured, not designed)

From the 2026-11-19 ideas list, in rough priority order.

**Stored columns (query and UI only):**

1. **Group-by view**: a toggle that rolls the game list into W-L rows by
   season, opponent or opponent conference. This is the biggest jump in
   usefulness after the five above.
2. **Both ranked / own rank**: "when both teams are ranked", and "when we're
   ranked" vs "when unranked", from `RankGoingIn` and `OpponentRankGoingIn`.
3. **Records going in**, from `WinsBefore`, `OpponentWinsBefore` and the loss
   columns:
   - Opponent with a winning record.
   - Undefeated going in.
   - Week 1 games are 0-0 and should match none.

   The duplicate NCAA games (11-17 audit, Check 2) inflate these for 22
   team-seasons until dedup.
4. **Over/under**: an O/U summary line (`TotalResult`) and a total-size band
   (`OverUnder`). Floor 2012.
5. **Game outcome margin**: one-score (<= 8) and blowout (>= 21), from
   `Margin`. Mostly a game-list filter, because filtering on the result
   makes the W-L summary circular.
6. **Result**: wins only / losses only. A game-list filter, with the same
   circularity caveat.
7. **Day of week**: Saturday vs weeknight (Tue/Wed/Thu/Fri), from
   `LocalDayOfWeek`. Same no-venue nulls as Month.
8. **Box-score splits**: e.g. rushed for 200+, won the turnover battle,
   outgained. Floor 2004 (team box tier), with known 2011, 2016 and 2018
   gaps. These are "how do we win" questions and only meaningful
   after the fact.

**Needs new data or columns:**

9. **Coach or era**: needs coach-by-season data. Check what the coach
   sourcing already stores before designing.
10. **Bowl vs playoff**: needs an event-type or round flag; phase 3 holds
    both.
11. **Rest days and bye week**: derivable from the previous game's kickoff.
    Needs a projector column, a version bump and a rebuild.
12. **Weather**: ESPN publishes it only for some games, and we don't store
    it on the fact table.
13. **Rivalry and trophy games**: a curated list; ESPN does not publish one.
14. **Neutral-site kind** (bowl vs kickoff classic vs relocated home game):
    `VenueId` exists, but the categories are fuzzy.
