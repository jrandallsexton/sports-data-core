# Player Pick'em

## Overview

Allow users to pick position players (QB, RB, WR, TE, K, etc.) as well as
teams (DEF) — much as is done for Fantasy Football.

However, there is **zero draft**. Users can pick any player even if others
in their league have picked them. No ownership, no waivers, no trades.

This is not a game based on drafting the correct players, but a game of
**knowing the best matchups from week to week**.

The thesis, in one exchange: "Yeah, you have Joe Smith — but he's going
against Michigan this weekend. I'm taking Joe Blow; they're playing
UAB." A stud facing a Top-10 defense can be the worse play than a
lesser-known player facing an overmatched opponent. Surfacing exactly
that judgment is the game — and the AI insights product.

**Tagline: "Know the matchups. Win the league."**

A "teaser" for this upcoming functionality will ship to both web and mobile
ahead of the feature itself to stir interest (see Teaser below).

---

## Competitive landscape and the design risk

Why this game shape is rarely seen — and why the empty space is both the
opportunity and the one real design risk.

**The adjacent products all exist; none occupy this quadrant:**

- **DFS (DraftKings / FanDuel)** — "no draft, any players, weekly
  lineups" ... but with a **salary cap and entry fees**. Closest
  mechanical neighbor.
- **Best Ball (Underdog, Yahoo)** — dropped in-season management but
  KEPT the draft; the draft is the product.
- **Player-prop pick games (PrizePicks, Underdog Picks, Sleeper
  Picks)** — over/under threshold picks; the shape this feature
  deliberately rejects (gambling-classification exposure).
- **Season-long fantasy (ESPN / Yahoo / Sleeper)** — the draft is the
  identity; scarcity/ownership is the whole game.
- Occasional free "pick a weekly lineup" promo games have existed, but
  nobody has made it *the* product with real league infrastructure
  (invites, discovery, standings, message boards) around it.

The quadrant this feature occupies — **no draft, no salary cap, no
money, league-social** — is genuinely sparse.

**Why the quadrant is empty (respect this): the salary cap in DFS is not
a monetization gimmick — it is the differentiation engine.** Without a
cap or scarcity, every rational player picks the consensus studs,
lineups converge, and the week is decided by whichever FLEX got lucky.
Convergence-to-consensus is the failure mode that has kept this game
shape unexplored.

Note the risk is **sport-asymmetric**: it is at its worst in the NFL
(shallow elite pool + industrial consensus rankings) and diminishes
sharply in NCAAFB (huge pool, no public college-fantasy consensus,
matchup-driven variance) — a major input to the NCAAFB-first decision
above.

**Why this design believes it survives anyway** (from the original
2026-07 analysis):

1. Shared slots are **neutral** slots — identical picks cancel, moving
   the contest to the slots where entries diverge. Convergence
   sharpens the skill test rather than breaking it.
2. Multi-slot rosters make FULL convergence combinatorially unlikely;
   identical complete lineups ≈ never.
3. DEF / FLEX / K are natural divergence points (DEF-by-matchup
   especially — a pure weekly knowledge play).
4. The framing is *stronger* without a cap: DFS tests "who optimizes
   under a constraint"; this tests purely **"who reads matchups
   better."** That is also exactly the question the AI insights
   product answers — which is why the subscription synergy is
   structural, not bolted on.

**But that is a hypothesis, and the backtest exists to test it.** Run
the draft scoring table over a past season; simulate a "consensus"
lineup (weekly stud-picks) against plausible divergent lineups; check
whether the leaderboard actually separates. Outcomes:

- Divergence pays → the empty quadrant was an oversight; we're early.
- Consensus dominates → tune the scoring BEFORE building UI
  (threshold/ceiling bonuses widen divergence vs. floor-heavy
  continuous scoring) and re-run.

Either way the dragon is confronted for the price of a query, right
after the athlete-stats audit confirms the data exists.

**Overall take:** justified excitement, one known dragon, and a cheap
test that either slays it or teaches us how to. The mechanic is
unproven, not disproven — and the pieces that make it viable here
(league infrastructure already built, college/NFL data pipeline, AI
matchup insights as the paid companion) are precisely the assets a
generic competitor lacks.

---

## Product decisions

### Decided

1. **Scoring model: performance points, NOT player props.**
   Players earn points from real statistical performance. Example: a QB
   passing for 200 yards scores X; reaching 300 scores Y. The scoring
   config supports both continuous accrual (points per yard/reception/TD)
   and threshold bonuses (hit 300 passing yards → bonus) — the exact rule
   set is a config decision, not an architecture decision.

   Deliberately rejected: over/under threshold *picks* ("will player X
   exceed N yards?"). That is structurally a player prop — the
   PrizePicks/Underdog shape that has drawn gambling-classification
   challenges in multiple states. Even free-to-play, it would change our
   store content-rating posture. Performance scoring sits inside the
   long-established fantasy-sports carve-out.

2. **Weekly lineups with carry-over.**
   Users CAN change their lineup every week, but selections carry over
   from the previous week as the starting point for the next. Starting
   every user from an empty lineup weekly would be a retention killer;
   carry-over means the default action is "tweak," not "rebuild."
   Implication: the weekly rollover job clones the prior week's lineup
   into the new week (excluding players on bye / inactive — see Open
   Questions), and users edit from there.

3. **No draft, unrestricted selection.** Any user may roster any player
   regardless of other members' choices. The skill expression is matchup
   evaluation, not scarcity management.

4. **Built on the existing league infrastructure.** This is a new league
   type on PickemGroup, not a parallel system. Invites, join policy +
   expiry, public discovery, pending-invitation cards, standings,
   message boards — all inherited. The delta is the pick entity (weekly
   lineup), the scoring engine, and the player-facing UI.

5. **NCAAFB first** (reversed from an initial NFL-first lean,
   2026-08-04). Three reasons, in order:
   - **The convergence risk is sport-asymmetric.** The NFL consensus
     machine (shallow elite pool, weekly rankings everywhere) makes
     stud-stacking trivial — the exact failure mode in Competitive
     Landscape below. College inverts every input: 130+ FBS teams,
     thousands of players, NO mainstream weekly fantasy consensus to
     copy, and a talent gradient that makes production violently
     matchup-dependent. The divergence the game needs occurs naturally.
   - **The v1 audience is the founder's own league — NCAAFB people.**
     The first season lives or dies on dogfooding; build where the
     test community actually plays.
   - **The college data pipeline is the moat.** College fantasy is a
     wasteland because of the data problem this platform spent 2.5
     years solving. Build where the unique asset is everyone else's
     barrier to entry.

   NFL follows once the game proves out (larger mainstream draw; also
   the sport where scoring-table tuning against convergence matters
   most). Costs accepted with NCAAFB-first: much larger player pool
   (a picker-UX problem — search + AI insights carry it) and roster
   churn/transfer-portal noise. The athlete-stat audit covers BOTH
   sports.

6. **AI insights are the companion product.** Player-matchup insights
   ("this WR faces a bottom-5 pass defense") are exactly the
   subscription content already planned (AI previews / DeetsMeter). The
   game creates weekly demand for the insights; the insights make the
   game winnable for engaged users. Free game, paid brainpower.

### Open

1. **Lineup shape** — proposed: 1 QB / 2 RB / 2 WR / 1 TE / 1 FLEX /
   1 K / 1 DEF (classic), configurable per league later. Needs a call on
   whether v1 offers config or one fixed shape (recommend fixed for v1).
2. **Lock granularity** — recommend per-player lock at that player's
   kickoff (a Thursday player locks Thursday; the rest of the lineup
   stays editable). A single weekly lock is simpler but makes Thursday
   slots dead weight. Per-player locking is real engine work; decide
   before schema design.
3. **Scoring rule set v1** — the specific points table (per-yard rates,
   TD values, threshold bonuses, DEF scoring). Needs a worked draft +
   validation against a few real 2025 box scores.
4. **Carry-over edge cases** — bye weeks, injured/inactive players,
   players who changed teams. Proposal: carry the player over regardless
   but badge the problem ("BYE", "OUT") loudly; auto-dropping surprises
   users more than a flagged empty-ish slot.
5. **Standings math** — weekly points summed season-long? Weekly
   head-to-head? Recommend cumulative points with weekly winners
   highlighted (matches existing pick'em standings mental model; no
   schedule pairing needed).

---

## Data requirements (the gating factor)

Scoring requires **per-player, per-game box-score statistics** in the
canonical store. Current known state:

- Player/athlete entities: sourced (Player service; ESPN athletes,
  including rosters via franchise-season).
- Team-level game statistics: canonicalized
  (CompetitionCompetitorStatistics).
- **Per-athlete game statistics: UNVERIFIED.** Whether ESPN's per-athlete
  event statistics documents are currently captured by Provider and
  processed into canonical rows by Producer must be audited before any
  timeline is promised. This is the first concrete task (read-only
  audit; see also docs/features/espn-processor-data-capture-audit.md).

If athlete box scores are not yet ingested, the long pole is:
Provider capture → Producer processor(s) → canonical player-game-stat
rows → scoring engine. If they are, the long pole shrinks to the scoring
engine + UI.

Not required for v1: injury reports, depth charts, projections, live
in-progress scoring (post-game scoring is fine; live comes later and
feeds the Command Center vision).

---

## Architecture sketch

New pieces (names illustrative):

- **League type**: PickemGroup gains a player-pickem variant (enum or
  discriminator — decide during design). All membership/discovery
  machinery reused.
- **Lineup entities**: PlayerLineup (group, user, week) +
  PlayerLineupSlot (position slot, athleteId, lockedUtc). Weekly
  rollover clones week N's lineup into week N+1 (carry-over decision).
- **Scoring config**: per-league scoring rule set (v1: one fixed rule
  set; schema shaped for per-league config later).
- **Scoring engine**: on game finalization, resolve each locked slot's
  athlete game stats → points; write per-slot and per-lineup results
  (mirrors the existing pick-scoring flow on game finalization).
- **API surface** (BFF, `/ui/leagues/...`): player search/browse with
  matchup context (opponent, opponent's positional rank later), lineup
  get/put, weekly + season standings.
- **UI**: lineup screen (web + mobile), player picker with matchup
  context, results/standings views. The picker is the largest UI item —
  it must answer "who SHOULD I pick this week?" which is where the AI
  insights slot in.

---

## Teaser (ships first, independent of everything above)

A "Coming Soon: Player Pick'em" card on the web + mobile home pages.
Design chosen (2026-08-04): the **lineup-slot banner** — an empty roster
row (QB filled in accent, the rest dashed) that shows the game rather
than describing it. Built entirely from existing tokens. Platform
difference: the web QB slot pulses; mobile renders it static
(deliberate — an infinite animation on a phone home screen costs
battery for no message gain). The field hash-mark flourish was cut
during preview.

- Slot row uses COUNTS, not duplicates: "RB ×2", "WR ×2" — density wins
  in an advertisement. The gameplay lineup UI uses individual slots
  (each is a distinct interactive target holding a distinct player).
- Copy direction: "Pick any players. No draft. No ownership. Just
  matchups." No dates, no betting-flavored language.
- Static/dismissible; no interactivity required for v1 of the teaser.
  (Optional later: an "I'm interested" tap for demand signal.)
- Mobile ships via EAS update (JS-only) — deliberately NOT part of the
  initial Play Store AAB; store submission is not gated on this.

---

## Sequencing

1. Play Store submission proceeds (independent).
2. **Audit**: per-athlete game-stat capture state (read-only, one
   session). Converts "a few weeks" into an estimate.
3. **Backtest**: draft scoring table run over a past season;
   consensus-vs-divergent lineup comparison (see Competitive landscape
   above). Validates "fun" before any UI exists.
4. Decisions: lineup shape, lock granularity, scoring table v1
   (informed by the backtest), carry-over edge cases, standings math.
5. Design doc v2 with schema + processor plan (await authorization
   before build).
6. Teaser ships (any time after 1; independent of 2–5). Status: built
   on web + mobile 2026-08-04, in tree awaiting bundle/PR.
