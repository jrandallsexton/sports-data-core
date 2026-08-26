# Player Pick'em — Roster Persistence

**Status:** Approved 2026-08-26 (operator). Supersedes the localStorage-only
draft shipped with the admin-preview roster builder (#674–#676).

## Decisions (operator-ratified)

| Decision | Choice | Rationale |
|---|---|---|
| Lock granularity | **Per-player**, at that player's kickoff | Doc open question #2. A Thursday player locks Thursday; Saturday slots stay editable. Weekly lock makes Thursday slots dead weight. |
| Lock rule | **Kickoff − 5 minutes**, via the shared `IsStartLocked` | Team picks lock 5 minutes before kickoff; rosters are no different. One lock rule in the product. |
| Lock storage | **Derived, never stored** | Schedule moves self-heal (`ContestStartTimeUpdated` exists because games move). Also makes the future commissioner option (weekly-lock mode) a group setting with zero schema change: evaluate every slot against the week's earliest kickoff instead of its own. |
| League game model | **`PickemGroup.GroupType` enum (`TeamPickem` = 0 / `PlayerPickem`) — one game per league** | Operator decision 2026-08-26, superseding an earlier companion-mode draft: Player Pick'em is a DIFFERENT GAME and lives in a DIFFERENT league — two scoring systems in one group is asking for trouble; invite the same friends to a second league instead. An enum rather than capability flags because the games are mutually exclusive: flags would make the invalid both-enabled state representable and force every consumer to police it, where the enum makes it unrepresentable. `TeamPickem = 0` so every existing row is correct by default. NOT a `PickType` value — `PickType` answers "how is a TEAM pick scored?" within a TeamPickem group and is referenced in ~85 files. Creation-flow support for PlayerPickem groups (the standalone-league vertical: game-type choice at creation; player leagues skip matchup filters/PickType/confidence, keep week structure, skip matchup slates and team-pick scoring) is the NEXT vertical; until then, `UPDATE "PickemGroup" SET "GroupType" = 1` is the admin substitute for E2E. |
| Carry-over | **Lazy clone on read** | On GET for week N+1: no lineup + week N exists → clone, re-resolving each athlete's new-week contest. Same user experience as the doc's "rollover job" without a fleet-wide Sunday job; clones only for users who show up. |
| Bye/departed athletes | **Carry and badge, never auto-drop** | Doc open question #4. A bye slot (`ContestId` null) never locks and never scores; the UI badges it loudly. |

## Entities (API database, beside the pick tables)

```text
PlayerLineup
  Id               Guid PK
  PickemGroupId    Guid
  UserId           Guid
  SeasonYear       int
  SeasonWeek       int
  Created/Modified audit fields
  UNIQUE (PickemGroupId, UserId, SeasonYear, SeasonWeek)

PlayerLineupSlot
  Id               Guid PK
  PlayerLineupId   Guid FK → PlayerLineup (cascade delete)
  SlotId           string(8)    'QB','RB1','RB2','WR1','WR2','TE','FLEX','K' ('DEF' reserved)
  -- athlete: soft canonical refs + render snapshot (the pick-table pattern)
  AthleteId        Guid         stable across seasons (stats-audit lesson:
                                never trust a roster-row's year)
  AthleteSeasonId  Guid         season row at save time; the scoring join
  Position         string(4)    FLEX-eligibility validation + badge
  FirstName, LastName, TeamName, TeamSlug
  -- the week's game: lock + scoring anchor
  ContestId        Guid?        null = bye at save time
  ContestStartUtc  DateTime?    denormalized start; lock derives from it
  OpponentName     string?
  Created/Modified audit fields
  UNIQUE (PlayerLineupId, SlotId)
```text

The v1 lineup shape (1 QB / 2 RB / 2 WR / 1 TE / 1 FLEX / 1 K) is fixed and
enforced server-side; `DEF` is reserved until the team-defense picker exists.

## Locking

A slot is locked ⟺ `IsStartLocked(slot.ContestStartUtc, now)` — the same
extension the team-pick submit path uses (locked when start ≤ now + 5 min).

Write validation on assign/replace/remove:
- slot id exists in the fixed shape
- position eligible for the slot (FLEX = RB/WR/TE)
- no duplicate athlete across the lineup's slots
- **target slot not locked** (can't swap out a player whose game started)
- **incoming athlete's game not locked** (can't add a player mid-game)
- bye athletes (no contest this week) are assignable and never lock

Staleness note: `ContestStartUtc` is denormalized, exactly like
`PickemGroupMatchup.StartDateUtc`. The slot updater should eventually join
the `ContestStartTimeUpdated` consumer family; v1 accepts the same staleness
the pick tables do. Flagged, not forgotten.

## Feed contract addition

`AthleteMatchupSummaryDto` gains `ContestId` + `ContestStartUtc` (already
joined in the Producer query, just not projected). The UI passes them on
save; the API **revalidates the lock from its own clock** — the client's
values are a convenience, never the authority.

## Endpoints (API, UI namespace; all gated on `GroupType == PlayerPickem`)

- `GET  /ui/leagues/{leagueId}/player-lineups/{seasonYear}/{week}/mine`
  → lineup + slots, each with derived `isLocked`; triggers the lazy clone
- `PUT  /ui/leagues/{leagueId}/player-lineups/{seasonYear}/{week}/mine/slots/{slotId}`
  → assign/replace (validations above)
- `DELETE …/mine/slots/{slotId}` → clear an unlocked slot

Requests against a TeamPickem league → 403 (one game per league). Membership
in the league is required (existing league authorization).

The slot rules are a server-side port of the UI's pure `rosterLogic`
modules — kept pure from day one for exactly this move.

## UI changes

- Roster builder swaps localStorage for the API (localStorage dies; the
  per-league draft keys were a stand-in for exactly this).
- Admin preview targets the first `PlayerPickem`-type league the user
  belongs to; a proper league picker comes with launch shape.
- Locked slots render disabled with a lock affordance; bye slots badge.

## Trust boundary (alpha)

The upsert accepts the athlete identity + display snapshot from the
client; the server authoritatively resolves the CONTEST ANCHOR from the
athlete's claimed TeamSlug. A malicious member could therefore submit a
locked athlete under an unlocked team's slug and slip a mid-game player
into their own lineup. Accepted for the alpha (founder + friends): the
blast radius is the cheater's own roster, and the planned scoring-side
audit closes it retroactively — scoring resolves each athlete's REAL
game server-side and invalidates slots whose stored anchor mismatches.
Full server-side athlete verification per save would mean re-fetching
the position feed on every write; deliberately deferred.

Concurrency: lineup writes carry no concurrency token — the only
writer of a lineup is its owner. The no-duplicate-athlete rule is
enforced by a unique (PlayerLineupId, AthleteId) index, so the one
meaningful same-user race (simultaneous saves of the same athlete into
different slots) is rejected by the database and surfaced as the same
validation error the pre-check produces. An xmin token adds an
untestable-under-InMemory failure path for negligible remaining gain;
revisit with the scoring integrity pass.

Week numbers are PHASE-SCOPED: within one season year the NFL counts
weeks 1-4 in preseason, 1-18 in the regular season, and 1-5 in the
postseason. Matchup resolution (GetMatchupsForSeasonWeek) filters on
SeasonPhase.TypeCode — default 2, regular season — so a bare week
number can never anchor a slot to a preseason or playoff game. A future
playoff/CFP pick'em passes TypeCode 3 explicitly.

**Alpha-blocking, tracked**: SEASON_YEAR/WEEK are pinned to 2026 week 1
in the UI. A server-resolved current week (or a week selector) must land
before the week-3/4 alpha — a user cannot reach later weeks without it.

## Future (explicitly out of scope here)

- Scoring engine (doc open question #3) — joins slots
  (`AthleteSeasonId`, `ContestId`) to `AthleteCompetitionStatistic`.
- Commissioner options: weekly-lock mode, roster shape config — live next
  to `GroupType`.
- `DEF` slot (team-defense picker), depth-chart-driven roster slimming
  (ESPN `/depthcharts` — the game-roster feed's `starter` is empty for
  college).
