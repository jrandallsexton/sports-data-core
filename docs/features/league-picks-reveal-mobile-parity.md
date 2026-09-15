# League Picks Reveal — Mobile Parity (By Week view)

Status: **In flight** — PR 1 (reveal enforcement + member roster) merged as
#736; readiness counts for the pre-lock state in PR #737. Design direction
chosen from the Claude Design handoff
(`docs/features/Fantasy Football Picks Interface/handoff/`): **D consensus
rail** as the pane, **A's card** as D's expanded state, **A3** pre-lock
readiness + **A4** spreads-off variant, **C drill-in** as a follow-up PR;
**B matrix** deferred pending friend feedback.
Last updated: 2026-09-07
Ask: the web Leaderboard's "By Week" view — a games × members matrix showing
every member's pick (team + confidence chip + ✓/✗) once games lock — has no
mobile equivalent. "People _want_ to see what others have picked." Possible
follow-on changes to the overall Standings view are undecided and out of scope
here.

## ⚠️ Finding that gates everything: the reveal is client-side only

`GET /ui/leagues/{id}/overview/{week}`
(`GetLeagueWeekOverviewQueryHandler.cs`) stamps
`IsLocked = StartDateUtc.AddMinutes(-5) <= UtcNow` onto each contest (line
~107) — and then appends **every member's picks for the whole week
unconditionally** (lines ~137–157).
`GetUserPicksByGroupAndWeekQueryHandler` filters only on group/user/week — no
lock predicate exists anywhere server-side (grep confirms). The only
enforcement is the web renderer dropping unlocked rows
(`LeagueWeekOverviewTable.jsx:40-41`).

**Anyone in a league can read every member's un-locked picks today by opening
dev tools.** The only gate is league membership (`ILeagueMembershipGuard`).

This must be fixed server-side regardless of mobile — and *before* mobile
ships, since a second client re-implementing a client-side filter doubles the
surface pretending the data isn't already leaked. Fix shape (small):

- In `GetLeagueWeekOverviewQueryHandler`, after computing `IsLocked` per
  contest, filter each member's picks to locked contests only — **except the
  requesting user's own picks**, which are always included (you may see your
  own pending picks).
- The lock rule (kickoff − 5 min) already exists in three independent copies:
  this handler, web `MatchupCard`, mobile `MatchupCard.tsx:74`. The server
  filter makes the server authoritative for *reveal*; the client copies remain
  for pick-entry UX only.
- Optional same-visit cleanup: the handler is a documented N+1 (2 queries per
  member per page view — `docs/audit/launch-readiness-2026-07.md:190`). One
  picks query for the whole league filtered by week would fix both.

Ship this as its own small API PR (PR 1). Web behavior is unchanged (it
already hides those rows); the payload just stops carrying secrets.

## Current state (explored 2026-09-07)

### Web
- `LeaderboardPage.jsx` — three tabs: Standings / All Weeks / By Week; season +
  league selectors; `showBots` filter drops `isSynthetic` rows client-side.
- By Week: `LeagueWeekOverviewTable.jsx`, fed by
  `GET /ui/leagues/{leagueId}/overview/{week}` →
  `LeagueWeekOverviewDto { contests[], userPicks[] }`.
  - `contests[]` (`LeagueWeekMatchupResultDto : ContestResultDto`): teams
    (short/slug/rank/FranchiseSeasonId per side), spreads, O/U, scores,
    winner ids, `isLocked`, `finalizedUtc`.
  - `userPicks[]` (`UserPickDto`): `userId, user, isSynthetic, contestId,
    franchiseSeasonId, pickType, confidencePoints, isCorrect, pointsAwarded`.
  - Cell team abbrev is DERIVED client-side by matching
    `pick.franchiseSeasonId` against the contest's away/home ids — the API
    sends no abbrev on the pick. Mobile must do the same derivation.
- Week list comes from `LeagueSummaryDto.seasonWeeks` on `GET /ui/leagues`
  (no extra call); phase-aware `SeasonWeekDetails` is also available.

### Mobile today
- `app/(tabs)/standings.tsx` — single Standings list (rank/player/points),
  `useStandings` → `GET /ui/leaderboard/{leagueId}`. No in-screen tabs, no
  By Week or All Weeks equivalent.
- **Missing**: client fns for `/overview/{week}` and `/scores`; types for
  `LeagueWeekOverview*` / `LeagueScoresByWeek`.
- **Reusable inventory (strong)**: `SegmentedControl` (in-screen tabs, already
  used in 4 screens); `LeagueWeekSelector` (league + week chip rows, takes
  `seasonWeeks`); `useSeasonLeagueSelection` (season/league reconciliation,
  mirrors the web page's logic); confidence badge + ✓/✗/🔒 markers in
  `MatchupCard`; `Card`/`EmptyState`/`LoadingSpinner`/theming; react-query +
  FlatList/RefreshControl patterns throughout.

## Mobile UX: the matrix doesn't fit a phone

Web is member-column-oriented (N members wide). Options:

**A. Per-game cards (recommended).** One card per locked matchup — header =
teams/spread/score (reusing MatchupCard's row idioms), body = one line per
member: name · picked-team chip · confidence badge · ✓/✗. Answers the actual
social question ("what did everyone pick for THIS game") in a native layout,
works at any league size, FlatList-friendly (mobile perf is THE priority).
Unlocked games render as a locked placeholder row ("locks Sat 11:55 AM") or are
simply omitted — web omits them.

**B. Horizontal-scroll matrix, frozen game column.** Closest to literal web
parity; worst small-screen ergonomics; heavier to build well (synced
scrolling); table perf risk with RN. Not recommended as v1.

**C. Per-member drill-in.** Tap a member in Standings → their week's picks.
Cheap and complementary, but doesn't answer "everyone vs. this game" at a
glance. Could be a later add on top of A.

Proposed screen structure: keep the `standings` tab, add a `SegmentedControl`
— **Standings | By Week** — mirroring web's tabs (All Weeks deferred, see open
questions). By Week pane = `LeagueWeekSelector`-style week chips + the
per-game card list.

## Work breakdown

**PR 1 — API: server-side reveal enforcement** (independent, security)
- Filter unlocked contests' picks (others' only) in
  `GetLeagueWeekOverviewQueryHandler`; fold the N+1 fix if cheap.
- Tests: unlocked pick excluded for others / included for self; locked pick
  included; lock boundary (−5 min) pinned with `IDateTimeProvider`.
- No web change needed.

**PR 2 — Mobile: By Week view**
- `leaguesApi.ts`: `getLeagueWeekOverview(leagueId, week)` + query-key entry;
  types `LeagueWeekOverview`, `LeagueWeekMatchupResult`, reuse `UserPick`.
- `standings.tsx`: SegmentedControl; By Week pane component
  (`LeagueWeekOverview` feature component + `GamePickCard`), week chips from
  `seasonWeeks`, member rows derived from `userPicks` grouped by `contestId`,
  team abbrev derived via franchiseSeasonId matching.
- Respect the existing patterns: `shouldShowGambling` gates the spread display
  (memory rule: ALL surfaces route through it); `isSynthetic` rows behind the
  existing Show Bots pill (`StandingsControls` already has one); theming via
  `getTheme`.
- Batch with other mobile work until EAS-worthy per convention.

## Open questions (operator)

1. **Layout**: option A (per-game cards) confirmed, or is literal matrix
   parity (B) wanted?
2. **All Weeks tab**: in scope for mobile now, or later? (Endpoint exists;
   it's a second pane + one client fn.)
3. **Unlocked games**: omit entirely (web behavior) or show a locked
   placeholder row with lock time (leans into anticipation)?
4. **Bots**: default hidden with a Show Bots pill (web parity), fine?
5. **Overall Standings view changes** — operator flagged "might change, not
   certain"; parked until decided.
6. **Timing**: perf-hardening window runs to mid-Sept; PR 1 (security) is
   arguably window-appropriate now, PR 2 is feature work.

## Related
- `docs/audit/league-authorization-idor.md` — membership guard history on
  these endpoints.
- `docs/audit/launch-readiness-2026-07.md:190` — the N+1 note.
- `docs/features/phase-aware-league-weeks.md` — week identity details.
- `docs/ui/matchup-card.md` — shared card idioms.

---

# Design brief — hand-off to Claude Design (self-contained)

You are designing a NEW screen pane for **sportDeets**, a sports pick'em
mobile app (Expo / React Native, iOS + Android). Produce visual mockups —
phone-frame, dark theme — for the layout options below, so the founder can
pick a direction. No code needed; visuals + rationale.

## Product context

sportDeets leagues are groups of friends picking winners of college/pro
football games each week, optionally with **confidence points** (each pick
carries a unique point value; correct = earn the points). The single most
requested social feature: **once a game locks (kickoff − 5 min), everyone can
see what everyone else picked.** Trash talk is the product. This pane is where
that happens.

The web app shows this as a desktop table: rows = games (away @ home, point
spread, final score), columns = league members, each cell = the member's
picked team abbreviation + a small circular badge with their confidence
points + a green ✓ or red ✗ once the game finishes. That table cannot work on
a phone — design the mobile-native answer.

## Where it lives

The app has a bottom tab bar (Home · Games · Standings · Profile). The
Standings tab today is a single ranked list (Rank / Player / Points). This new
pane joins it behind an in-screen segmented control:

```
[ Standings | By Week ]
```

Above the pane: existing chip-row selectors (League: "Curbstomp" ·
Week: Wk 1, Wk 2, …). Design the By Week pane content; show the segmented
control and chips for context.

## Data available per game (locked games only)

- Away/home team short names ("SJSU", "USC"), national rank when ranked
- Point spread relative to a side (e.g. "USC −37.5") — NOTE: some users
  disable gambling content; provide one variant with no spread shown
- Game state: upcoming-but-locked · live (score) · final (score, winner)
- Per member: display name, picked team, confidence points (1–N, unique per
  member per week), and — once final — correct/incorrect and points awarded
- Members who did NOT pick a game: show as a muted "no pick" state
- Leagues have 2–12+ human members, plus optional bot members (hidden by
  default behind a "Show Bots" toggle)

## Options to visualize (at least these three)

**A. Per-game cards (current favorite).** Vertical list, one card per locked
game. Card header: teams + spread + score/status. Card body: one row per
member — name · picked-team chip · confidence badge · ✓/✗. Consider: how the
card compresses for 10+ members (collapse? top-N + expand?), how "everyone
picked the same team" reads at a glance (group by side? majority bar?), and a
"you" highlight.

**B. Compact matrix.** Frozen first column of games; horizontally scrolling
member columns. Show how it looks mid-scroll and how a cell reads at phone
size.

**C. Per-member drill-in.** Standings rows tappable → a member's full week
(their picks list with results). Show the entry point and the detail screen.

Also welcome: a hybrid or a fourth idea if something better emerges — e.g. a
per-game "consensus" summary (pie/split bar of who took which side) with
member detail on tap.

## States to cover (pick one option, likely A, and show these)

1. Mid-week: mix of final games (✓/✗ visible), one live game, remainder
   locked-not-started
2. Pre-lock: nothing revealed yet — empty/anticipation state ("Picks reveal at
   kickoff — first lock Sat 11:55 AM")
3. Gambling-content-off variant (no spreads anywhere)

## Visual identity

- Dark theme first: near-black/navy background, white text, cyan/teal accent
  (the web uses cyan headers on #0b0e13-ish backgrounds), green for correct,
  red for incorrect
- Existing idioms to keep: confidence points in a small filled circle
  (~20×20, bold number); green ✓ / red ✗ result marks; 🔒 for locked-no-pick;
  team chips use short names (no real team logos — text/color only, this is a
  hard licensing constraint)
- Density matters: users check this Saturday morning through Sunday night;
  a full 8-game slate × 6 members should be scannable without feeling like a
  spreadsheet

## Sample data for realism

League "Curbstomp", Week 1, members: Aesop, James Sexton, Ranman, SRVIVR,
sportDeets (you). Games: SJSU @ USC (USC −37.5, final 26-42), UAPB @ MIZ
(MIZ −55.5, final 14-54), IDHO @ UTAH (UTAH −38.5, final 14-66), MIA @ STAN
(MIA +24.5, final 45-6), ORST @ HOU (HOU −21, final 20-33). Example picks:
Aesop took USC (32 pts, ✗), UAPB (31, ✓), UTAH (30, ✓); Ranman took IDHO
(10, ✗), MIA (22, ✓); James Sexton skipped several games (no pick).
