# StatBot Advisor — standings-aware pick help

**Status**: backend slice BUILT 2026-09-25 (planner + advice endpoint, 32
unit tests); web dialog BUILT 2026-09-25 (`StatBotAdvisorDialog.jsx`,
robot button left of the pick-mode badge, replace-all apply through the
normal submit path). Not yet: private provenance stamp, commissioner
toggle (both need a migration), mobile ·
**Build order**: web first, mobile parity after (2026-09-25) ·
**Origin**: owner's note, Week 4 — users who are falling in the standings are
already wondering what to pick; let StatBot help, tuned to where they sit.

## The ask (owner, verbatim intent)

- A robot icon just left of the league-type badge (ATS|SU) on the mobile
  picks header. Tapping it opens a "StatBot help" dialog.
- Step one: an analysis of the user's current standing in the league
  (e.g. ATS + confidence points, 3rd place, averaging 44/week against the
  leader's 56).
- Step two: a football-flavoured offer of help — Hail Mary, Prevent, QB
  Draw, Goal-line — where the play the user chooses sets their risk level.
- StatBot then suggests picks based on that selection.

## Rule zero: the advisor is blind

**The advisor never reads, infers from, or reasons about any other member's
picks — current week or past.** What it may know about other members is
their **performance and standings**: totals, weekly averages, accuracy,
rank, and scored weekly results. Nothing else. No exception for locked
games, bots, or the commissioner.

For the current week this is a fairness invariant, not a privacy nicety:
the picks reveal is server-enforced to lock time (PR #736) precisely so
nobody has this information, and an advisor with a back door would be the
app cheating on the user's behalf. For past weeks it is simplicity (owner,
2026-09-25): past picks add nothing the standings do not already carry — a
high weekly average in a confidence league already says "chalk-heavy
picker" — and one invariant is easier to enforce and explain than two.

Enforcement is by construction, not by discipline. The planner is a pure
function whose inputs are: the week's `ContestPrediction`s, the
leaderboard (totals, averages, accuracy, rank), scored past-week results
(points only), and **the caller's own** picks (to know what is locked). It
has no parameter through which another member's pick could arrive, and the
advice endpoint's queries never touch `PickemGroupUserPick` for any user
but the caller. A unit test pins that.

Everything below that says "the field" means *an assumption about
behaviour*, never an observation of picks.

## What already exists (this is mostly composition)

The advisor does not need a new prediction engine. Every piece below is
live today:

| Piece | Where | What it gives the advisor |
|---|---|---|
| Per-matchup model probabilities | `ContestPrediction` (SU + ATS, `WinProbability`, `ModelVersion`); already on the wire as `predictions[]` per matchup (`GetLeagueWeekMatchupsQueryHandler`) | The signal for every pick and for ranking confidence points |
| LLM preview pick | `MatchupPreview.PredictedStraightUpWinner / PredictedSpreadWinner`; surfaced as `aiWinnerFranchiseSeasonId` (robot icon on the card) | A second opinion; the consensus lab may replace it later |
| StatBot as a league member | `StatBotPickWriter` writes StatBot's own picks from the preview, confidence ranked by predicted margin, N..1 | StatBot's *actual* standing in this league — an honest credibility marker |
| Styled synthetic pickers | `SyntheticPickService` + `SyntheticUserPickStylesConfig` (`conservative` / `moderate` / `aggressive`, spread-keyed thresholds) and `ReconcileConfidenceAsync` (rank by distance from 0.5, respect locked picks) | Risk styles and confidence allocation, already unit-tested |
| Standings | `GetLeaderboardQueryHandler` → `LeaderboardUserDto` (`TotalPoints`, `WeeklyAverage`, `Rank`, `LastWeekRank`, `IsSynthetic`) | The analysis step, almost verbatim |
| Plan-then-apply batch flow | `PickImportPlanner` + mobile `ImportPicksModal` | The exact UX shape: preview a full sheet, then apply |
| Gambling gate | `shouldShowGambling(pickType, options)` | Whether the reasons may mention spreads |

So the new work is: (1) a standings analysis, (2) an *allocation strategy*
that maps a risk level onto the model's probabilities, (3) a dialog. The
engine is a variant of what the bots already run against themselves.

## The core idea: standings decide how much variance you need

"Help me with my picks given my standing" reduces to one question: **how
much variance does this user need this week?**

- Expected points per week are maximised by taking the model's side in
  every game and ranking confidence by certainty. That is exactly what
  StatBot does as a member. If everyone did that, nobody would ever pass
  anybody.
- If you are behind, expectation does not catch you up; **variance** does.
  In a confidence league the only way to make up 12 points in a week is to
  cash a high-point pick that most of the league did not — and the only
  proxy the blind advisor has for "what most of the league does" is the
  model's probability itself. A 78% side is where the field goes; a 52%
  side is a coin flip the field splits on.
- Being different is cheapest where the model is least sure. Flipping a
  55/45 game costs ~10 points of expectation per 100 staked; flipping an
  80/20 game costs 60. So risk is spent on the near-coin-flips first.

The risk level sets how many games get flipped and where the big points
go. Standings tell the advisor which level to *recommend*.

### Sizing the deficit

**Per game, not per week** (owner, 2026-09-25): the number of games in a
league varies week to week, so a weekly average and a per-week deficit do
not compare across weeks. Points per game does. It is now on the
leaderboard DTO (`PointsPerGame` = points over decided picks) and is the
variable the recommendation reads.

From the leaderboard plus the league's schedule:

```
deficit        = leader.TotalPoints - me.TotalPoints
weeksLeft      = regular-season weeks on the SEASON CALENDAR (Season service
                 overview) whose end is still ahead; null if unreachable
                 — never the league's own week rows, which only exist once
                 matchups sync ("1 week left" on a full-season league)
gamesThisWeek  = this week's open games                        ← the ONLY game count
neededPerWeek  = deficit / weeksLeft                           (internal; never shown)
unit           = (stddev of points-per-game across scored member-weeks,
                  falling back to 15% of the leader's points per game)
                 × gamesThisWeek                               = a typical week's swing
```

`neededPerWeek / unit` gives the recommendation as before.

**Two owner rules (2026-09-25).** (1) Future slates are never forecast:
matchups sync about a week ahead, and a ranked-teams league (AP Top 25 +
SEC) has no predictable slate size at all. Only this week's games are a
fact, so the analysis carries `GamesThisWeek` and `MaxPointsThisWeek`
(confidence values 1..N minus what locked picks hold) and nothing about
games beyond this week. Weeks left IS a known count of league weeks and is
shown. (2) No unattainable target is ever displayed: an earlier draft said
"34 per game to catch up" in a league where that is impossible. Instead
the copy names something reachable, from standings arithmetic alone
(rule zero intact), **with the people ahead scoring at their pace** —
points per game × this week's games — never at zero (owner, 2026-09-25:
"a perfect week takes the lead" had assumed the leader scored nothing).
`CanCloseGapThisWeek` = deficit + leader's expected week < this week's
max; `BestCaseRankThisWeek` = one plus the members whose total plus
expected week still meets the caller's total plus the max; the member
directly ahead is shown with the gap and their expected week. "You're 5th, 714 behind
Aesop. 21 games this week, up to 231 on the table. 9 weeks left in the
regular season. A perfect week moves you to 3rd at best. Dave in 4th is
40 points ahead."

`neededPerWeek / unit` gives the recommendation: below 1 unit Goal-line,
below 2 QB Draw, above that Hail Mary; a non-positive deficit is always
Prevent; an unknown horizon (calendar unreachable) stays neutral and never
goes past QB Draw. Thresholds live in `PickAdvisorOptions`, validated at
startup, tuned later.

Pick types: the deetsMeter only carries StraightUp and AgainstTheSpread
numbers, so the endpoint refuses Over/Under (and None) leagues with a
validation message and the web button is hidden for them, rather than
handing back a sheet of "no model number" rows.

Open nuance, not yet decided: in a confidence league the points available
per game also scale with slate size (a correct pick in a 15-game week is
worth ~8 on average, in a 6-game week ~3.5). Per-game is far better than
per-week, but "share of available points" would be the fully normalised
version. Revisit once real leagues show whether it matters.

## The decisions (name them after we count them)

Owner's direction: settle the number of distinct decisions first, then get
clever with the football names. So, what does the dial actually vary?

Two levers, and only two:

1. **How many games to flip** away from the model's side, always taking the
   lowest-certainty games first: none · one · a few · every game under a
   certainty threshold.
2. **Where the top confidence points go**: on the locks (certainty-ranked,
   N..1) or on the flipped games.

Not every combination is coherent. Flipping many games and then giving them
*low* points is pure expectation loss with no variance gain, so "many
flips, points on the locks" is out. Flipping nothing makes lever 2 moot.
What survives:

| # | Flips | Top points on | Character | Recommended when |
|---|---|---|---|---|
| 1 | none | locks | Pure expectation; StatBot's own sheet | Leading, or gap closable by expectation |
| 2 | one | locks (flip gets a mid value) | One upset, low stakes | Behind by < ~1 weekly stddev per week |
| 3 | a few (2–3) | locks (flips get upper-middle values) | Several upsets, stakes still protected | Behind by 1–2 stddevs per week |
| 4 | every coin-flip (< ~60%) | **flips** | Swing for the week | Behind by more, or few weeks left |

Four coherent settings. If four does not read on a phone card, #2 and #3
merge (flip count becomes a function of the deficit) and it is three. The
football names then map onto whichever count wins; the working names
Prevent / Goal-line / QB Draw / Hail Mary fit the four-row table in that
order.

Why #4 puts the big points on the flips: the variance of a confidence
score is the sum over games of `c² · p(1−p)`, so it is the high-point
coin-flips that swing a week. The certainty-ranked sheet is the *lowest*-
variance sheet possible, which is the last thing a trailing user needs.

ATS leagues: the spread has already equalised the sides, so "favourite"
means nothing and every probability sits near 50%. The dial still works —
it reads distance from 0.5 on the ATS prediction — and the existing
spread-keyed style thresholds cover the "spread too big to trust the model"
case the bots already handle.

Non-confidence leagues: only lever 1 applies. Levels still mean "how many
dogs," which is the whole lever those leagues have.

## Which number drives the pick: deetsMeter vs StatBot's preview

Owner: they frequently disagree; needs discussion. Framing for that:

The advisor needs two things per game: a *side* and a *certainty*.
deetsMeter has both (a side and a probability). The LLM preview has a side
and a projected margin, no calibrated probability.

| Option | Side from | Certainty from | Note |
|---|---|---|---|
| A | deetsMeter | deetsMeter | Simplest; preview shown as "agrees / disagrees" in the reason |
| B | LLM preview | deetsMeter distance from 0.5 | Matches StatBot's own league sheet (which is preview-driven), so setting #1 really is "StatBot's sheet" |
| C | Agreement decides | deetsMeter when they agree; **disagreement = treated as a coin flip** | Disagreement *is* information about uncertainty; those games become the first candidates to flip |
| D | Consensus panel | panel | Not available until the lab's Phase 4 evidence gate |

Recommendation: **C**, with deetsMeter as the number. It uses the
disagreement instead of hiding it, it costs nothing, and it degrades to A
when no preview exists. One consequence to accept: StatBot the *advisor*
and StatBot the *league member* would not always hold the same pick on a
disputed game. If that inconsistency is unacceptable, B is the fallback.

Whatever is chosen, the reason line states it plainly: "deetsMeter 71% —
lock, StatBot agrees" / "deetsMeter 78% but StatBot disagrees — treated as
a coin flip."

**Naming rule (owner, 2026-09-25):** the probability is the **deetsMeter**
(MetricBot's model); **StatBot** IS the preview and its pick. Copy must
never call the number "StatBot" — an early draft did.

## UX (web first, mobile parity after)

Owner (2026-09-25): build on web first. The backend logic and the dialog
flow will change while we learn; iterating on web avoids paying the mobile
test loop for every change. Mobile is a parity pass once the flow settles.
The dialog is modelled on `ImportPicksDialog.jsx` (web) — the existing
plan-then-apply pattern — and later on `ImportPicksModal` (mobile).

1. **Entry**: the lucide `Bot` glyph (already StatBot's mark on
   `PickButton.jsx`) immediately left of the `pick-mode-badge` span in the
   `PicksPage.jsx` header. Mobile later: `MaterialCommunityIcons
   name="robot"` left of the mode badge in the `headerRight` pill of
   `app/(tabs)/picks.tsx`. Hidden when the week has no unlocked games, the
   user is not a member, or the league has bot help off.
2. **Analysis card** (top of the modal): rank and movement, total vs
   leader, points per game vs leader (weekly average shown as the familiar
   number, per-game as the honest one), weeks and games remaining, needed
   per game, and StatBot's own standing in this league. If StatBot is
   *behind* the user, say so — it is funny and it is honest.
3. **Level picker**: one card per setting with the football description,
   **StatBot's recommendation pre-selected** and labelled "StatBot's call."
   One line under each in plain language ("one upset, low stakes" / "swing
   for the week").
4. **Suggested sheet**: every unlocked game — the pick, the confidence
   value, and a one-line reason composed client-side from structured facts
   (same principle as spread context: numbers from queries, never prose).
   "Already kicked off — untouched" for locked games. Spread mentions
   respect `shouldShowGambling`.
5. **Apply**: one button. **V1 replaces every unlocked pick** the user has
   for the week (owner's call: the ideal is to run it before picking at
   all, and partial-merge gets tricky fast). Locked games never change.
   Writes through the existing submit path so validation (distinct 1..N,
   deadline) stays in one place. Nothing is ever applied without the tap;
   the button copy says it replaces.
6. **Provenance, private**: stamp the applied picks with the level used
   (`PickemGroupUserPick` already carries `SyntheticPickStyle`; a sibling
   `AdvisedLevel` or reuse of that column). **Never surfaced to other
   members** — not in the By Week reveal, not on chips, not in standings.
   It exists for the user's own view, for SmackBot (whose pushes go only
   to the user), and for measuring whether the advisor helps.

## Where it computes

Server side, one endpoint, so web and mobile share the engine:

```
GET /ui/picks/{groupId}/week/{week}/advice?level=Prevent|GoalLine|QbDraw|HailMary   (or 1..4; omit = recommended)
→ PickAdviceDto { recommendedLevel, level, analysis, picks[], flipCount, coinFlipCount, lockedCount, noPredictionCount }
   picks[]: { contestId, headline, kind: Lock|Lean|Flip|Locked|NoPrediction, franchiseSeasonId,
              confidencePoints, modelProbability (for the advised side), previewAgrees, isCoinFlip, differsFromExisting }
```

Built as: `PickAdvisorPlanner` (pure: `BuildSheet`, `RecommendLevel`),
`PickAdviceService` (composition), `GetPickAdviceQueryHandler`, endpoint on
`PicksController`, tunables in `PickAdvisorOptions` (bound from
`SportsData.Api:PickAdvisor`, all defaulted). The slate comes from the same
`GetLeagueWeekMatchupsQueryHandler` the picks page uses, so the advisor
sees exactly the matchups, model numbers, and preview side the user sees.

Inputs, and only these: that slate, the leaderboard, scored past-week
results (points and pick counts), the league schedule (for games
remaining), and the caller's own picks. See rule zero. The service's only
`UserPicks` query is caller-scoped; `PickAdviceServiceTests.RuleZero_…`
pins that other members' current-week picks cannot change the output.

No LLM call: v1 is deterministic, free, and hallucination-proof. An
LLM-written "coach's note" is a later add once the consensus panel is
proven — same sequencing rule as the preview prompt ("model predicts, LLM
explains").

The allocation logic should be extracted from `SyntheticPickService` into
a pure, tested `PickSheetPlanner` that both the bots and the advisor call,
rather than a second copy of the confidence-ranking rules.

## Guardrails

- **Blind.** Rule zero, enforced by the planner's signature and a test.
- Always labelled as StatBot's suggestion, with StatBot's record in this
  league beside it. Never auto-applied, never applied to locked games.
- Games with no prediction sort last and are left blank with a reason, not
  guessed.
- Available to every member equally. **Commissioner toggle in v1, default
  on** (owner's call) — a league setting, not a user one.
- Pick'em is not gambling; the advisor speaks in win probability. Lines
  appear only where the gate already allows them.

## Decisions (owner, 2026-09-24)

- Count the decisions first, name them after. (Levers and the four
  coherent settings above are the proposed count.)
- StatBot recommends a level; it is pre-selected.
- V1 replaces every previous unlocked pick. No merge.
- Commissioner toggle ships in v1, defaulting to on.
- Advised picks are never shown as such to other members.
- Other members' inputs are performance and standings only, never their
  picks, past or present (2026-09-25).
- The recommendation is sized per game, not per week; points per game is
  exposed on the leaderboard (2026-09-25).
- deetsMeter vs preview: open; framing above, recommendation C.

## Phasing

1. **Engine + web dialog**: advice endpoint, analysis, levels, recommended
   level, sheet, replace-all apply, commissioner toggle. Extract the planner
   from the synthetic service. Private provenance stamp. The still-open
   questions (setting count, deetsMeter vs preview, thresholds) get settled
   *during* this phase as the flow takes shape — start with four settings
   and option C as working defaults, expect to change them.
2. **Calibration + mobile parity**: weekly-score stddev for the
   recommendation thresholds, measure advised-vs-unadvised outcomes, then
   port the settled flow to `app/(tabs)/picks.tsx`.
3. **Voice**: SmackBot situation for advised picks; optional coach's note
   from the preview pipeline once the panel is proven.

## Open questions (owner)

Owner (2026-09-25): undecided on all three; expecting clarity from building.
Working defaults for phase 1 in brackets.

- Four settings or three? (See the decisions table; the answer decides the
  names.) [four]
- deetsMeter vs preview: A, B, C, or wait for D? [C]
- Certainty threshold for "coin flip" (~60%) and the flip count for
  setting #3: fixed constants, or scaled by slate size (a 6-game NFL week
  vs a 15-game NCAAFB week)? [fixed constants in one config block, so
  scaling is a later change in one place]
- ~~Past-week habits of other members in phase 2?~~ Resolved 2026-09-25:
  **entirely blind** to other members' picks, past and present. Only their
  performance and standings are inputs.
