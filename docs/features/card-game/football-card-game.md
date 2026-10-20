# Football Card Game

Status: playable in the sportDeets mobile app (open to every user, D27) as a casual,
pass-the-time game (D18); the browser prototype stays as the testing aid. It uses no sportDeets data. Card art resources will live
alongside this doc in `docs/features/card-game/`.

Revision history:

- 2026-10-18: concept expansion with a dice-based core loop.
- 2026-10-19: dice removed in favor of a banked stamina ramp (see the appendix for what
  changed and why). Playable browser prototype.
- 2026-10-20: the game goes into sportDeets mobile, admin-only at first. The prototype
  gained a phone layout (section 13). Read yards (D20). The React Native build: engine
  port, game screen, profile entry (section 13). Then: Huddle removed (D26) and the game
  opened to every user (D27).

Every rule here is **provisional**: a starting point for playtesting, expected to change
as the game is played. Section 7 tracks which choices the owner has confirmed and which are
still working assumptions.

## 1. The seed

From the original note:

- 5 possessions per player.
- Offensive cards: **Sticky Fingers** (cannot be targeted by an interception card when
  needing more than 5 yards for a first down), **4th and OMG** (score a TD if 4th & 1 on
  your side of the field).
- Defensive cards: **Gotchya** (draw the offense into movement -- false start).
- Power Plays, which work like Uno's power cards (Draw Four, Skip): **PunchBall** (defender
  punches the ball out, immediate turnover), **Hijacked** (leaping interception).
- Open items: custom graphics for every card, clear rules and play process, a rules engine.

The seed cards are all **exceptions**: they override, cancel, or short-circuit something.
That is the right instinct for Uno-style fun, but it needs a base engine to be exceptions
*to*. Most of this doc is about that engine.

## 2. Design pillars

1. **Football-literate, not a simulation.** Downs, distance, field position, and
   play-calling mind games matter. No clock management, personnel, or formations.
2. **Card play is the skill.** No dice. Outcomes trace back to the cards both players
   chose and how they spent their stamina.
3. **Every snap is a decision for both players.** Offense and defense both commit
   something every snap, so nobody waits through the other player's turn.
4. **Swingy but earned.** Power Plays should feel like Uno's Draw Four -- dramatic and
   occasionally unfair -- but stamina-gated so they land at big moments.
5. **Fast.** A game (5 possessions each) should take 15-20 minutes on paper and under 10
   digitally.
6. **Paper-prototypable.** Index cards and a scratch pad for stamina before any code.

## 3. Core loop

### Game structure

- 2 players. A coin flip decides who receives first; possessions alternate, so each player
  gets 5.
- **Every possession starts at the offense's own 25** (75 yards to go). Field position does
  not carry over between possessions.
  - Why: fixed possessions plus carried-over field position would make punting and "who got
    the short field" a second game layered on top of the first. Fixed starts keep every
    possession an equal, self-contained puzzle -- the same reasoning college overtime uses.
  - Consequence: **there is no punting.** On 4th down you go for it or kick a field goal.
    That makes 4th-down cards (4th and OMG) a real part of the game, not a novelty.
- A possession ends on a TD, a field goal attempt, a turnover, a turnover on downs, or a
  safety.
- After a TD: kick the PAT (automatic, 1 point) or go for 2 (one snap from the 3).
- The highest score after 10 possessions wins. Ties go to college-style overtime: each side
  gets one possession from the opponent's 25, with a mandatory 2-point try from the third
  overtime round on. Ties are common enough to need this; see section 8.

### Stamina

Stamina is the single resource. It pays for the play card and any modifiers or Power
Plays played alongside it.

- **Level.** Starts at 1 each drive. Rises by 1 after each successful play (one the defense
  did not stuff), up to 5. A stuffed play leaves it unchanged. Defensive
  counter cards can lower it.
- **Bank.** Starts each drive at 1. Playing cards spends from the bank. **Unspent stamina
  carries to the next snap.** After every snap -- successful or stuffed -- the
  bank gains stamina equal to the current level. The bank is capped at 6.
- **The bank resets at the start of each drive.** Nothing carries between possessions.

Example: snap 1 starts with a bank of 1. You play a 1-cost run (bank 0). It succeeds, so
the level rises to 2 and the bank gains 2: you start snap 2 with 2. You play a 1-cost
pass plus nothing else (bank 1). The defense stuffs it, so the level stays at 2 and the
bank gains 2: you start snap 3 with 3.

### The snap

1. **Defense commits** a read face-down: **Run**, **Pass**, or **Blitz**.
2. **Offense commits** face-down: exactly one play card, plus any modifier cards, with a
   total cost no greater than the bank, or a field goal attempt.
3. **Reveal** both.
4. **Resolve:**
   - **Correct read = held.** The play gains only its **read yards** -- the smaller number
     printed on every play card, often 0 (D20) -- and the offense level does not rise. "Stuffed"
     in this doc and in card text means a correct read. On a 2-point try, a correct read stops
     the play outright.
   - **Wrong read** = the printed yardage, and the offense level rises by 1.
   - **Blitz** counts as a Pass read. When it is correct, the defense may play **Sack**.
     When it is wrong against a Run, the Run gains +3 bonus yards: the cost of guessing
     aggressively.
5. Reactive windows open (section 4), then **spot** the ball and collect bank income.

The order matters. **The defense must commit before the offense reveals.** With no dice,
the hidden read is the only source of uncertainty in a snap. A defense that responds after
seeing the offense's card always wins.

### Huddle (removed)

The offense used to be able to Huddle: burn the down, draw a card, collect bank income.
It was removed on 2026-10-20 (D26). The 0-cost base plays mean the offense always has a
legal play that also lets the bank grow, so a Huddle only gave up a down for one card --
an easy button to hit by accident and a sure way to annoy a player who hit it on purpose.

### Field goals

Field goals are deterministic. A kick is good if the kick distance (the yards to the end
zone plus 17) is 40 yards or less. Spending 2 stamina extends the range to 50 yards.
Icing the Kicker removes the bonus.

## 4. Cards

### Decks and hands

Each player owns three decks plus a few reusable cards (D15, D16):

| Deck | Contents | Drive start | During the drive | Hand max |
|---|---|---|---|---|
| **Play deck** (offense) | Play cards only (32) | Draw up to 5 | Draw 1 after each play | 7 |
| **Offense specials** | Modifiers, Power Plays, counters (28) | Draw up to 3 | Draw 1 on each first down | 5 |
| **Defense specials** | Everything the defense plays except reads (35) | Draw up to 4 | Draw 1 after each snap | 7 |
| **Reusable** | Offense base plays (Inside Zone, Check Down); defense reads (Run, Pass, Blitz) | Always in hand | Return to hand after use | -- |

- **Why the offense has two decks:** play cards and specials compete for the same hand
  slots. Every special added to a single deck makes a dead opening hand (no affordable
  play) more likely. In simulation (section 8), mixing 18 specials into the play deck cut
  scoring from 2.31 to 0.89 points per drive. Separate decks let the specials list grow
  without touching the yardage economy.
- **Why base plays:** even with a separate play deck, about 10% of drives open with no
  1-cost play, which used to force a Huddle on 1st down. The two 0-cost base plays (2 yards each)
  guarantee a legal play, and they mirror the defense's always-available reads. They also
  open a banking line: a 0-cost play spends nothing, so the bank grows.
- When a deck runs out, shuffle its discard pile to form a new deck. Decks persist across
  drives; they are not rebuilt each drive (D13).

### Offensive play cards: the yardage curve

Every play card has a cost, a type (Run or Pass), printed yardage, and **read yards**: what
it still gains when the defense reads it correctly (D20, 2026-10-20). The expanded 32-card
play deck, shown as yards / read yards, with copies in parentheses:

| Cost | Run | Pass | Copies |
|---|---|---|---|
| 0 | Inside Zone 2/0 (reusable) | Check Down 2/0 (reusable) | -- |
| 1 | Dive 4/2 (5), QB Sneak 2/1 (2) | Quick Out 3/2 (4) | 11 |
| 2 | Off Tackle 7/3 (3), Draw 9/1 (2) | Slant 8/3 (3), Screen Pass 6/1 (2) | 10 |
| 3 | Power Sweep 11/3 (2), Reverse 15/0 (1) | Dig 13/3 (3) | 6 |
| 4 | Toss Sweep 18/2 (1) | Seam Route 20/5 (2) | 3 |
| 5 | -- | Go Route 35/0 (2) | 2 |

**Read yards shape each play's personality.** Short plays are reliable: Dive, Quick Out,
and QB Sneak keep half or more. Mid-range plays keep about a third. Long and trick plays are
boom-or-bust: Go Route and Reverse keep nothing, and Toss Sweep keeps 2. Seam Route is the
exception, a costly play with a real floor (5), which makes it the premium "safe big play."
Copy-weighted, read yards are about 22% of printed yardage. Base plays keep 0, so they stay
a guaranteed legal play, not an efficient one.

Why: with a correct read worth 0 for every play, the guess decided every snap and card
quality didn't matter when it was tested; spending stamina on a bigger play bought nothing
against a correct read. The owner rejected that as unlike football: a correct call limits a
play, it doesn't erase it.

The specialty plays still give the offense answers to specific reads:

- **QB Sneak:** short yardage; its 1 read yard was the original floor mechanic.
- **Draw and Screen Pass:** beat Blitz. A Blitz read doesn't stop the Screen, and both
  gain +6 against it.
- **Reverse:** inverts the read. A Run read doesn't stop it; a Pass or Blitz read holds it
  to 0.

Together these are the counter-design against "always read Pass on 3rd & long."

An uncontested drive (4 + 8 + 13 + 20 + 35 = 80 yards) scores in 5 snaps. The defense's
reads are what lengthen drives.

### Defensive stamina

Without its own budget, the defense's counters would be free.

- The defense bank starts each opponent drive at 1.
- It gains 1 per stop: a correct read, a sack, or a negative play. Cap: 5.
- Defense cards spend from this bank. Like the offense's, it resets each drive.

This makes momentum a **tug-of-war**. A stalling drive is literally the defense building
toward Hijacked or PunchBall (each cost 3). Those Power Plays become affordable on 3rd &
long after two stops -- exactly when they happen in real football. "The defense smells
blood" becomes a mechanic, and the defense always has a resource to manage.

The defensive economy is less tested than the offensive one: the simulation used coin-flip
reads and no defense cards.

### Timing windows

| Window | When | Typical cards |
|---|---|---|
| **Drive** | Start of the possession | Hurry-Up, persistent effects |
| **Pre-snap** | After both sides commit, before reveal | Gotchya, Hard Count, 4th and OMG |
| **Post-reveal** | After the reveal, before resolution | Audible, Play Action, Stunt |
| **Post-snap** | After resolution, before the spot | PunchBall, Hijacked, Sack, counters |

In each window the offense may play one card, then the defense may respond with one. There
are no infinite response chains -- Uno-simple, not Magic: The Gathering-deep. A **Counter**
card is the only exception: it can be played in response to a card in the same window.

### Card categories

- **Play cards** (offense only): carry type and yardage. Exactly one per snap.
- **Modifiers**: cheap tweaks to a snap or a drive, for either side.
- **Power Plays**: expensive, dramatic, outcome-replacing -- the Uno Draw-Four/Skip analog.
- **Counters**: cancel a specific Power Play -- the Uno Reverse analog. They are what make
  Power Plays interactive instead of just mean.

## 5. The seed cards

| Card | Side / type | Cost | Window | Rules text |
|---|---|---|---|---|
| Sticky Fingers | Off, modifier | 1 | Post-reveal | For the rest of this possession, the defense cannot play interception cards on any snap with more than 5 yards to go |
| 4th and OMG | Off, Power Play | Whole bank (minimum 3) | Pre-snap | Play on 4th & 1 on your own side of the 50. Touchdown |
| Gotchya | Def, modifier | 1 | Pre-snap | False start: the offense's committed cards return to hand, -5 yards, offense level -1. Replay the down; the defense may change its read |
| PunchBall | Def, Power Play | 3 | Post-snap | After a Run that was not stuffed gains yards: fumble, turnover |
| Hijacked | Def, Power Play | 3 | Post-snap | After a Pass that was not stuffed: intercepted, turnover |

Notes:

- **Sticky Fingers** is a conditional shield, so it implies that interception cards are
  the defense's main answer to obvious passing downs -- a readable cat-and-mouse on 3rd &
  long. It lasts the whole possession because a one-snap shield would almost never be
  timed correctly. It now competes with the play card for the bank, which is exactly the
  "play your cards correctly" tension.
- **4th and OMG** is a 51-to-99-yard automatic touchdown, and with no punting, 4th & 1 on
  your own side comes up often. It needs 1 copy, the whole bank as its cost, and a counter
  (Goal Line Stand). Reaching 4th & 1 with 3+ in the bank means a good drive that stalled
  -- a fitting trigger. This is the card people will tell stories about; keep it until
  playtesting says otherwise.
- **Gotchya** drains the level as well as yardage. A false start killing momentum fits the
  theme, and it gives the defense a cheap momentum tool. It cannot push the offense back
  past its own 1.
- **PunchBall and Hijacked** split along Run and Pass, so each is the defense's answer to a
  play type. Neither can fire on a stuffed play, so the defense is rewarded for guessing
  *wrong* in a sense -- it lets the play happen, then takes the ball. Watch whether that
  feels clever or cheap in playtests.

## 6. Deck composition

`cards.json` holds the exact rules text, cost, window, copies, and art brief for every
card. This section is the map of what each card is *for*; the JSON is the source of truth
(section 11).

**Offense specials (28 cards, 19 designs):**

| Role | Cards |
|---|---|
| Read manipulation | Hard Count, Audible, Play Action, Hot Route |
| Momentum and bank | Hurry-Up, Two-Minute Drill, Fired Up |
| Yardage | Broken Tackle |
| Interception protection | Sticky Fingers |
| Power Plays | 4th and OMG (legendary), Hail Mary, Fake Punt, Flea Flicker, Swinging Gate (2-point try) |
| Kicking | Big Leg |
| Counters | Ball Security, Pass Interference, Silent Count, Roughing the Passer, Flag on the Play |

**Defense specials (35 cards, 22 designs):**

| Role | Cards |
|---|---|
| Read coverage | Stunt, Zone Blitz, Goal Line Stand |
| Yardage limits | Bracket Coverage, QB Spy, Prevent Defense, Holding Call |
| Momentum and bank | Gotchya, Crowd Noise, Sack, Rally |
| Information | Film Study |
| Turnovers and scores | PunchBall, Hijacked, Strip Sack, Pick Six (legendary), Kick Six (legendary) |
| Kicking | Icing the Kicker, Blocked Kick |
| Counters | Read the Fake, Timeout, Flag on the Play (also Goal Line Stand, Prevent Defense) |

**Answer map:** every Power Play, plus the sharpest modifiers, has at least one answer.

| Power Play | Answered by |
|---|---|
| 4th and OMG | Goal Line Stand |
| Hail Mary, Flea Flicker | Prevent Defense |
| Fake Punt, Swinging Gate | Read the Fake |
| PunchBall | Ball Security |
| Strip Sack | Ball Security, Roughing the Passer |
| Hijacked, Pick Six | Pass Interference; Hijacked is also blocked by Sticky Fingers |
| Sack | Roughing the Passer; prevented by Hot Route |
| Gotchya, Crowd Noise | Silent Count |
| Hurry-Up, Two-Minute Drill | Timeout |

Still uncountered: Blocked Kick and Kick Six. Big Leg partly offsets Icing the Kicker, but
nothing answers a block. Holding Call can only be negated by Flag on the Play. Both are
candidates for the next pass.

**Scoring sources outside possessions:** Pick Six and Kick Six give the defense points,
which helps break ties.

**Fixes in this pass:**

- **Play Action was backwards.** "Your Pass is treated as a Run for the read" meant a Run
  read *stuffed* it -- the opposite of a play-action fake. It now reads: "If the defense
  read Run, your Pass gains double its printed yardage."
- **The 2-point try had no cards.** Swinging Gate (offense) and Read the Fake (defense)
  now cover it.

## 7. Decision log

| # | Decision | Status | Notes |
|---|---|---|---|
| D0 | No dice; outcomes come from the cards played | Owner-confirmed 2026-10-19 | Shifts the emphasis to card play |
| D1 | Stamina ramp keyed to the **drive**, not the down | Owner-confirmed 2026-10-19 | A down-keyed ramp resets to 1 on every first down, punishing success |
| D2 | Stamina is **banked** within a drive | Owner-confirmed 2026-10-19 | -- |
| D3 | Income = current level; the level rises by 1 per successful play (max 5) | Working | Required for banking to work: a flat +1 income with spending never grows (0 points per drive in simulation) |
| D4 | Stuffs and Huddles still earn income; they just don't raise the level | Working | Success-only income collapses scoring to 0.31 points per drive. Momentum losses come from counter cards |
| D5 | Bank cap of 6 | Working | Scoring barely changes (2.29 -> 2.25); the average peak bank drops from 6.7 to 4.1 |
| D6 | One play card plus any modifiers per snap | Working | Budget-splitting is the core skill |
| D7 | The bank resets each drive | Owner-confirmed 2026-10-19 | Carrying the bank across drives is a possible later experiment |
| D8 | Huddle allowed, not forced (forced only with no playable card) | Superseded by D26 | Saving is viable but not dominant in simulation |
| D9 | ~~Stuffed play = 0 yards (not partial)~~ | Superseded by D20 | Partial stuffs (50% of yards) pushed scoring to 4.6-5.6 points per drive; D20's floors are much smaller (about 22%) |
| D10 | Defense banks +1 per stop, cap 5, resets each drive | Working, **untested** | The simulation had no defense cards |
| D11 | Fixed starts at your own 25; no punting | Working | -- |
| D12 | 5 possessions each, plus college-style overtime | Working | About a 10% tie rate |
| D13 | Decks persist across drives; reshuffle the discard pile when a deck runs out | Owner-confirmed 2026-10-19 | The 35-card defense specials deck cycles about every third drive |
| D14 | Card frame: build on 2a, add 1a/1b's window strip and 2c's corner cost index; color = category, trim = side; an icon or shape for every color cue; show copy count, not rarity words | Owner-confirmed 2026-10-19 | See section 10 |
| D15 | The offense has separate Play and Specials decks | Owner-confirmed 2026-10-19 | Mixing specials into the play deck cut scoring from 2.31 to 0.89 points per drive |
| D16 | Two reusable 0-cost base plays (Inside Zone, Check Down, 2 yards each) | Owner-confirmed 2026-10-19 | Eliminates dead opening hands (10% -> 0%); scoring 2.34 points per drive with the expanded deck |
| D17 | Specials draw rates: offense 3 at drive start, +1 per first down, max 5; defense 4 at drive start, +1 per snap, max 7 | Working, **untested** | Not simulated; tune in paper tests |
| D18 | The game ships inside the sportDeets mobile app, not as a separate app | Owner-confirmed 2026-10-20 | Something for users to do while passing the time |
| D19 | Admin-only until further notice | Superseded by D27 | Uses the app's existing `me?.isAdmin` gate; there is no feature-flag system |
| D20 | Every play card prints **read yards**: a correct read holds the play to them instead of 0 | Owner-confirmed 2026-10-20 (numbers are a draft) | See the yardage curve in section 4. Rough CPU-vs-CPU scoring went from about 1.0 to about 1.8 points per drive |
| D21 | A correct read still holds the offense's level and banks +1 for the defense, even when read yards are gained | Working | The defense won the snap, just not completely |
| D22 | No read yards on a 2-point try: a correct read stops it | Working | Keeps the try a pure guessing duel; with floors, Seam Route (5) would convert even when read |
| D23 | Finer reads (inside/outside run, short/deep pass) | **Deferred** 2026-10-20 | Possible "advanced" mode later; adds complexity |
| D24 | The game screen keeps its light paper-and-ink look in dark mode | Working | It's a card table; the faces are designed as printed cards |
| D25 | The off-side hand is a chip in the hand's last section header, not its own row | Working | On a real phone that row's 44px decides 4 vs 5 cards across (section 13) |
| D26 | No Huddle | Owner-confirmed 2026-10-20 | Base plays cost 0, so there's never a reason to give up a down for one card; players would hit it by accident or regret it |
| D27 | Open to every signed-in user | Owner-confirmed 2026-10-20 | Get feedback from friends; there is no gate in the app |

## 8. Simulation findings

A quick Python Monte Carlo, 60k-200k trials per row. The offense is greedy (plays the
highest-yardage affordable card) unless noted. The defense reads by coin flip and has no
defense cards. **Directional, not balanced** -- real players read better than a coin flip,
and the defense card layer is not modeled.

### Format: tie rates

Using realistic per-drive outcomes (30% TD, 14% FG):

| Possessions each | Tie rate | Average margin | Decided by 14+ |
|---|---|---|---|
| 3 | 17.5% | 6.0 | 11% |
| 4 | 13.2% | 7.0 | 16% |
| **5** | **10.6%** | **7.8** | **20%** |
| 6 | 8.6% | 8.6 | 24% |
| 8 | 6.5% | 9.9 | 30% |

Five keeps games close but ties about 1 in 10, so overtime is required. If games run long,
4 possessions only raises the tie rate to 13%.

### Deck structure (2026-10-19)

The earlier stamina runs assumed an offense deck of play cards only. Adding specials
changes that. The setup here is greedy offense, coin-flip reads, deterministic field
goals, and specials modeled as dead weight in the hand.

| Offense deck setup | Pts/drive | Forced Huddles/drive | Drives opening with no legal play |
|---|---|---|---|
| One deck: 30 plays + 18 specials | 0.89 | 1.46 | 29% |
| One deck: 32 plays + 28 specials (expanded) | 0.45 | 1.87 | 35% |
| One deck, expanded, + base plays | 0.62 | 0 | 0% |
| **Split decks**, 30-play curve | 2.31 | 0.38 | 11% |
| **Split decks**, 32-play expanded curve | 2.15 | 0.34 | 10% |
| **Split decks**, expanded + base plays | **2.34** | 0 | 0% |

This drives D15 and D16. Specials don't help in this model -- they are pure dilution -- so
the one-deck numbers somewhat overstate the damage. But the opening-hand math is exact: a
5-card hand from a 48-card deck with 10 one-cost plays has no 1-cost play 29% of the time.

### Stamina economy

| Model | Offense policy | Pts/drive | TD | Snaps |
|---|---|---|---|---|
| **Banked, income = level, stuffs/Huddles earn** | Greedy | **2.29** | 30% | 7.0 |
| Same | Saver (Huddles toward big cards) | 1.94 | 25% | 6.7 |
| Same, bank cap 6 | Greedy | 2.25 | 29% | 7.0 |
| Banked, Huddles don't earn | Greedy / saver | 2.09 / 0.97 | -- | -- |
| Banked, only successes earn | Greedy | 0.31 | 4% | 4.7 |
| Banked, flat +1 income | Any | 0.00-0.06 | 0% | -- |
| Refresh (unspent lost), earned ramp | Greedy | 1.88 | 24% | 6.6 |
| Refresh, time-based ramp | Greedy | 0.74 | 7% | 6.0 |

Read-rate sensitivity (refresh model, greedy):

| Correct read rate | Stuff = 0 yards | Stuff = 50% of yards |
|---|---|---|
| 35% | 3.88 | 5.60 |
| 50% | 1.86 | 4.60 |

Takeaways:

- **The chosen model hits the target.** Banking with income equal to the level lands at
  about 2.3 points per drive (real football's rough rate) with the starting yardage curve
  unchanged.
- **The read dominates everything.** A 15-point swing in read accuracy doubles scoring.
  With no dice, the read *is* the game. Real players will read better than a coin flip on
  obvious downs, so expect to make the curve slightly hotter (raise the 2- and 3-cost
  yardage first) once humans play.
- **Starving income kills the game.** Momentum swings should come from counter cards
  (Sack, Crowd Noise, Gotchya), not from the base read.
- **Pacing:** about 7 snaps per drive x 10 drives = about 70 snaps a game. That fits the
  15-20 minute target on paper.

## 9. Rules engine

A card game with timing windows is exactly where ad-hoc `if` statements rot. The shape I
would use:

**Deterministic state machine plus effects as data.**

- **GameState**: an immutable record -- score, possession number, offense, down, distance,
  yard line, `level` and `bank` per side, hands, deck order, the shuffle seed, the current
  phase/window, and active persistent effects (for example, "Sticky Fingers active until
  the possession ends").
- **Actions**: `CommitRead`, `CommitPlay` (play card plus modifiers),
  `PlayCard` (in a window), `Pass` (decline a window), `KickFieldGoal`. `apply(state,
  action) -> state + events[]` is a pure function. Illegal actions -- over budget, wrong
  window, condition not met -- are rejected with a reason.
- **Phases**: `DriveStart -> DefenseCommit -> OffenseCommit -> PreSnap -> Reveal ->
  PostReveal -> Resolve -> PostSnap -> Spot/Income -> (next snap | PossessionEnd) -> ... ->
  GameEnd | Overtime`.
- **Cards as data, not classes**: each card is `{ id, side, cost, window, condition,
  effects[], duration, tags[], counters[] }`, using a small vocabulary of effect primitives:
  - `ModifyYards(n | factor)`
  - `ModifyLevel(side, n)`
  - `ModifyBank(side, n)`
  - `StuffOverride`
  - `TreatAsType(Run | Pass)`
  - `ReplaceResult(TD | Turnover | FirstDown | ...)`
  - `Penalty(yards, replayDown)`
  - `RevealRead`
  - `GrantImmunity(tag, condition, duration)`
  - `Cancel(cardTag)`

  Sticky Fingers is then `GrantImmunity(tag: "interception", condition: distance > 5,
  duration: possession)`. New cards are mostly JSON, and balance changes never touch engine
  code.
- **Tags** (`interception`, `fumble`, `penalty`, `power-play`) let immunities and counters
  target families of cards without naming each one.
- **The only randomness is the shuffle.** A seed in the state makes every game exactly
  replayable from (seed, action log). That gives replays, bug reproduction, anti-cheat (the
  server re-runs the log), and "share this game" links. Deterministic resolution also makes
  AI opponents much simpler.
- **Event log** (`ReadCommitted`, `PlayRevealed`, `Stuffed`, `CardPlayed`, `Countered`,
  `Touchdown`, ...) drives the UI animations and the play-by-play text.

**Tech:**

- Write the engine as a standalone TypeScript package with zero dependencies, so the same
  code runs in a React Native client (offline vs AI) and on a small authoritative server.
- Write a simulator harness against it from day one. Balance work is mostly "change a
  number, run 100k drives."
- **Async play first.** Committing face-down maps cleanly to async: each side submits, and
  the server reveals when both are in. "Your opponent committed -- your move" via push
  notification suits casual mobile better than real-time lobbies.

## 10. Card art

Resources will live in `docs/features/card-game/`. "Custom graphics for every card" is the
biggest cost driver, so contain it:

- **One frame template per category** (Play, Modifier, Power Play, Counter), each with its
  own color, plus an offense/defense trim and a rarity treatment. The art box is the only
  thing that varies.
- **Fixed layout**: name, cost pips, window icon, type badge (Run/Pass, on play cards),
  yardage (on play cards), rules text, flavor line, art. Consistent window icons let
  players read the timing at a glance.
- **Avoid real IP.** No NFL/NCAA marks, team names, uniforms, or player likenesses. Use
  generic/fictional teams and stylized athletes. A cartoon style (an exaggerated leaping
  interception, the PunchBall fist) fits the Uno tone and is cheaper to keep consistent.
- **Prototype with text-only cards.** Add art after the rules stabilize -- many cards will
  be cut or renamed in the first playtests.

### Frame review (2026-10-19, `Power Cards.dc.html`)

The file shows five frame directions on two seed cards: 1a/1b (v0), 2a retro trading
card, 2b broadcast, and 2c party card.

**The rules text is from the retired dice model.** Before the next design pass:

- "Doesn't stop a snake-eyes INT from the yardage table" should go -- there is no
  yardage table any more.
- "Spends all your Momentum" becomes "Spends your whole bank."
- The 1a/1b window strip shows `DRIVE / PRE-SNAP / REVEAL / ROLL`. It should be `DRIVE /
  PRE-SNAP / POST-REVEAL / POST-SNAP`.

Feed the next turn from `cards.json` (section 11), not from the doc.

**Direction (owner-confirmed 2026-10-19, D14): build on 2a, and borrow from two other variants.**

- **2a** has the strongest hierarchy: the biggest art box, a slab name ribbon, and a
  collector number already printed. It also reads like a real card at 320x448.
- **Borrow 1a/1b's four-segment window strip.** It replaces the "PLAYED: POST-REVEAL"
  text. Highlighting the active window lets a player read timing at a glance -- the most
  useful functional element in the whole set.
- **Borrow 2c's corner index** (cost, top-left, mirrored bottom-right). In a fanned hand,
  only the corners show, which is why Uno cards work. 2c's bottom-right "1" is rotated
  180 degrees and reads as an "L"; 4th and OMG is missing its bottom corner entirely.
- **2b** (dark broadcast) looks good but is text-heavy, gives the art the least room, and
  prints poorly. Keep it as a possible dark theme for the digital version.
- **2c's** oval wastes art area, and the tilted title collides with the art and the
  rules box.

**Gaps the next design turn should cover:**

1. **The play card frame -- the most important one.** 30 of the 48 offense cards are play
   cards, and none are designed yet. They need the yardage number to be the hero element
   and a Run/Pass type badge.
2. **Side versus category.** Right now green = Offensive and red = Power Play, but Power
   Plays exist on both sides (PunchBall and Hijacked are defense). Use the frame color for
   the **category** (modifier, power play, counter, play, read) and a trim or badge for the
   **side** (offense/defense).
3. **Colorblind safety.** Red/green is the most common deficiency pair. Pair every color
   cue with an icon or shape: a category glyph, plus an offense/defense trim pattern.
4. **Read cards and backs** (201-203, 901-903). The three read cards must share an
   identical back (903) so they can't be told apart face-down.
5. **Rarity wording.** "COMMON / LEGENDARY" is collectible-pack language, and the plan is a
   fixed deck. Show the copy count (`x2`, `1 PER DECK`) as the primary signal, with rarity
   only as the visual treatment (foil or gold for legendary).

## 11. Card metadata and asset naming

**`cards.json` in this folder is the source of truth for every card.** Claude Design
consumes it, the rules engine will consume it, and the doc tables in sections 5 and 6
describe it. When rules text changes, change the JSON first.

### Naming convention

- **`id`**: a 3-digit string, **permanent, never reused**. It is the only key tooling uses
  to join an image to its metadata. If a card is cut, its id is retired; if it is renamed,
  its id stays.

  | Range | Contents |
  |---|---|
  | 101-139 | Offense play cards (101-107 Run, 111-116 Pass) |
  | 141-149 | Offense base plays (reusable) |
  | 151-199 | Offense specials (modifiers, power plays, counters) |
  | 201-249 | Defense reads (reusable, not in the deck) |
  | 251-299 | Defense specials |
  | 301-349 | Either side |
  | 901-999 | Non-card assets (card backs) |

  The ranges show where a card was *born*. A card that later changes category keeps its id.
- **`slug`**: a kebab-case name for humans (`sticky-fingers`). It may change if the card is
  renamed.
- **Image files**: `{id}_{slug}_{asset}[_v{n}].png`
  - `asset` is `art` (the illustration that goes in the art box) or `card` (the full
    rendered card);
  - example: `151_sticky-fingers_art.png`, `151_sticky-fingers_card_v2.png`;
  - underscores separate fields, because slugs use hyphens;
  - tooling matches on the leading id only, so a stale slug in a filename is cosmetic, not
    a broken link.
- **Claude Design image slots**: `art-{id}` (for example, `art-151`). The current canvas
  uses mixed slot ids (`art-sticky-fingers`, `art-2a-sticky`); standardize on the next
  design turn.
- **Suggested layout**:
  - `card-game/cards.json`
  - `card-game/art/` -- `{id}_{slug}_art*.png`
  - `card-game/cards/` -- `{id}_{slug}_card*.png`
  - `card-game/design/` -- `*.dc.html` canvases

### Schema (v2)

| Field | Type | Notes |
|---|---|---|
| `id` | string | See above |
| `slug`, `name` | string | `name` is the printed title |
| `side` | `offense` \| `defense` \| `either` | `either` cards appear in both specials decks (see `copies`) |
| `category` | `play` \| `modifier` \| `power-play` \| `counter` \| `read` \| `back` | Drives the frame |
| `cost` | `{kind: "fixed", value}` \| `{kind: "all", min}` \| `null` | `all` = spends the whole bank (4th and OMG) |
| `window` | `commit` \| `drive` \| `pre-snap` \| `post-reveal` \| `post-snap` \| `null` | Play cards and reads are `commit` |
| `play` | `{type: run \| pass, yards}` \| `null` | Present on play cards, and on Power Plays that act as the play card (Hail Mary `yards: null`, Flea Flicker) |
| `replacesPlay` | bool | Power Plays that resolve instead of a normal play (4th and OMG, Hail Mary, Fake Punt, Flea Flicker, Swinging Gate) |
| `rulesText` | string | The printed rules |
| `reminderText` | string | Small print: clarifications, counter notes |
| `flavorText` | string | The italic quote |
| `tags` | string[] | Used by immunities and counters (`#interception`, `#fumble`) and printable on the card |
| `counters` / `counteredBy` | id[] | `counteredBy` is derived from `counters` -- keep them in sync |
| `copies` | `{deck: count}` | Decks: `offense-plays`, `offense-specials`, `offense-reusable`, `defense-specials`, `defense-reusable`. Backs are `{}` |
| `rarity` | `common` \| `rare` \| `legendary` | Visual treatment only (fixed deck, no packs) |
| `art.brief` | string | One-line illustration prompt |
| `assets` | `{art, card}` | Expected filenames, derived from the convention |
| `status` | `seed` \| `draft` \| `proposed` \| `cut` | `seed` = from the original note; `proposed` = depends on an unconfirmed decision (base plays: D16) |

The top level also carries `deckTotals` (copies per deck) and `conventions`. **v1 -> v2:**
`copies` changed from `{offense, defense, perPlayer}` to a per-deck map, the 141-149 range
was added, and the backs gained 904 (offense specials).

A future engine will add an `effects` array (section 9). Claude Design can ignore it.

### Current contents (2026-10-19, schema v2)

- 59 card designs plus 4 backs (901 offense plays, 902 defense specials, 903 reads, 904
  offense specials).
- Copies: 32 offense plays, 28 offense specials, 35 defense specials, 2 offense base plays,
  and 3 defense reads.
- Status: 5 seed, 54 draft.
- Copy counts are first guesses.
- **Card backs:** the base plays share the play-deck back (901), so a committed base play
  is indistinguishable from a real play. The reads share back 903.
- **Deck lifecycle (D13):** decks persist across drives, and the discard pile is
  reshuffled when a deck runs out.

## 12. Playable prototype

`play.html` in this folder is a browser playtest build: a full game (5 possessions each,
plus overtime) against a CPU opponent. You play offense on your possessions and defense on
the CPU's.

- **Data-driven:** every card's name, cost, window, copies, and printed text comes from
  `cards.json`. Card *behavior* is a handler per card id in the page's script, so a new
  card id needs a handler before it does anything.
- **Running it:** open the published artifact (claude.ai/artifact/D49ZA8eSttFzbXPZucbY5U),
  or serve this folder (`npx serve` or `python -m http.server`) and open `play.html`.
  Opening the file directly fails because browsers block `fetch` of `cards.json` from
  `file://`.
- **`#autoplay`** runs CPU vs CPU with no delays. It is useful as a smoke test: 15 headless
  games on 2026-10-19 completed with 0 script errors.

**Rules interpretations the prototype made** -- these are its choices, not settled
decisions:

1. **Drive-window cards** (Hurry-Up, Fired Up, Film Study, Rally) can be played at the start
   of *any* snap, not just the first snap of the drive.
2. **The offense commits only its play card face-down.** Modifiers are played in their
   windows afterward (the doc's original wording had modifiers committed alongside the
   play).
3. **Counters answer the card they name in any window.** Flag on the Play can answer any
   post-snap card, or be played as your own post-snap card.
4. **Follow-ups:** Pick Six and Strip Sack are extra defense plays right after Hijacked or
   Sack resolves, outside the one-card-per-window limit.
5. **Field goals** can be attempted on any down. The kick gets only a pre-snap window (Big
   Leg, Icing the Kicker, Blocked Kick, Kick Six).
6. **2-point try:** one snap from the 3. A gain of 3+ is good; a correct read stops the play
   (no read yards, D22); a turnover just
   fails (no defensive return). Overtime rounds 1-2 are drives from the 25, then a 2-point
   shootout.
7. **A play that comes back** (Gotchya, 4th and OMG, Fake Punt) returns to hand, and its
   cost is refunded.
8. **Broken Tackle** replaces a read Run's read yards with its printed yardage minus 3 (only
   when that's more). The play still counts as read for the level.

**First CPU-vs-CPU read (15 games, 174 drives):** about 1.0 point per drive, 61% of drives
ending on downs, 46% of snaps stuffed. The CPU defense guesses the CPU offense's tendencies
well, so these numbers say more about the AI than about the game. Human playtests decide
balance.

**Implementation note:** the prototype uses an async turn loop rather than the pure state
machine in section 9. That was faster to get playable. The card handlers are already
isolated by id, which is the seam for a later engine extraction.

## 13. Mobile (sportDeets)

### Phone layout (prototype, 2026-10-20, second pass)

`play.html` switches to a phone layout at widths of 600px or less. On desktop, append
`#phone` to the URL to see it in a 390x844 frame. The layout is the blueprint for the React
Native screen.

The first pass (dock with tabs, a single sideways-scrolling card row, a large play-log box, a
separate meters row) was rejected by the owner: the log used too much space, the cards too
little, the meters row was mostly empty, and sideways scrolling slows play. The second pass
is built around the hand:

1. **Score bar:** names, scores, down and distance, spot. Stamina sits under the team it
   belongs to as compact chips -- `LVL` and `BANK` under the offense, `DEF` under the
   defense -- so there is no separate meters row.
2. **Field strip:** a thin bar with the line of scrimmage, first-down line, and ball.
3. **Snap row:** the read and play cards -- always the same size as the cards in hand
   (owner call, 2026-10-20), and counted when the hand's card size is chosen -- then a short
   feed of the current snap's
   last few lines (or the previous snap's result until something happens). Active effects
   show as small flags. Tapping the feed opens the full log.
4. **Prompt bar:** the question and its buttons (Kick, Pass, No response...). Selecting a
   card shows its full rules here, with a **Play** button if it's playable.
5. **Hand (the rest of the screen):** every card in the active hands is visible at once, no
   sideways scrolling. Offense: Plays (hand + base plays), then Specials. Defense: Reads,
   then Specials. The grid uses the biggest cards that fit -- 3 across, else 4, else 5 --
   and starts right under the prompt bar; spare height makes the cards taller (up to 5:9)
   instead of leaving a gap (owner call, 2026-10-20). Playable cards are outlined in
   gold; when a prompt is active, the rest are dimmed. Play cards show yardage; other cards
   show the start of their rules.
6. **Off-side hand:** collapsed into one row (for example "Defense specials · 3") that opens
   a view-only sheet. Film Study adds a "Their plays" row. The app build makes these chips
   in the hand's header instead (D25).

**Playing a card:** one tap on a gold (playable) card plays it -- no confirmation (owner
call, 2026-10-20: confirmation slows experienced players). Long-press any card to read its
full rules in the prompt bar without playing it; tapping a card that isn't playable does the
same. A **Confirm plays** option (on the start screen and in the full-log sheet, off by
default, saved per device) restores tap-to-select, tap-again-to-play for players learning
the deck. In the app this becomes a game setting.

### Integration plan

Built 2026-10-20 (not yet committed or shipped). It follows the existing app
conventions (expo-router, layer-organized code, Jest 29):

| Piece | Location | Notes |
|---|---|---|
| Engine and CPU | `src/utils/cardgame/` | A TypeScript port of the prototype's turn loop and card handlers. Pure: no React, no I/O. The `ask()` seam stays: the engine awaits a decision function, so the UI (or a test) supplies the human's choices. Seeded RNG for reproducible tests. |
| Card data | `src/utils/cardgame/cards.json` | Copied from this folder at build time (or imported). This folder stays the source of truth. |
| UI | `src/components/features/cardgame/` | Score bar, field strip, meters, table, prompt bar, dock, card sheet -- the layout above. Uses `getTheme(useColorScheme())` and `AppText`. |
| Route | `app/card-game.tsx` + a `Stack.Screen` in `app/_layout.tsx` | Full-screen, outside the tabs. |
| Entry point | `app/(tabs)/profile.tsx` | A "Games" section, open to everyone (D27). |
| Game state | Local component state (`src/hooks/useCardGame.ts`); a zustand store saved to AsyncStorage if resume-after-backgrounding is wanted | No backend. Leaving the screen abandons the game. |
| Tests | `__tests__/utils/cardgame/` | Engine tests with a seeded RNG and scripted decisions; a CPU-vs-CPU smoke test replaces `#autoplay`. |

**What was built:**

- **Engine** (`src/utils/cardgame/engine.ts`): a near-line-for-line port of the
  prototype. `createGame()` returns the state, the current `prompt` (the human's pending
  decision), `resolve()`, `run()`, and `abandon()`. Seeded RNG (`seededRng`). The
  CPU-vs-CPU test replaces `#autoplay`; 40 seeded games averaged 1.5 points per drive.
- **Card data:** `src/utils/cardgame/cards.json` is a copy. **When `cards.json` here
  changes, copy it to the app.**
- **Entry:** a "Games" section on the profile tab, open to every signed-in user (D27).
- **Layout** (`handLayout.ts`, pure and tested): the prototype's fit rule as arithmetic
  rather than DOM measuring.

**Changes from the prototype layout,** found by doing the math for a real phone (an
iPhone's 844pt minus the status bar and home indicator leaves about 590pt for the snap row
and the hand), where the prototype's numbers fell to 5 cards across:

1. **The off-side hand is a chip** in the last section's header ("DEF 4 ›"; under Film
   Study, "THEIRS 5 ›" too), not a row (D25). Deck counts moved to the log sheet.
2. **Prompt buttons share the title's row** and wrap only when they don't fit.
3. **Stable columns:** the column count comes from the prompt bar's resting height. While a
   card is inspected (its rules make the prompt taller), the cards get shorter -- never
   below 5:7 -- instead of the grid jumping from 4 across to 5.
4. **The snap row's cards share the stretch**, so they stay the hand's size (owner call)
   and spare height is spread over one more row.

Checked in a 390x751 frame on Expo web: 4 across on offense, 3 across on defense with
three specials, no dead space, no clipped text.

**Calls made for the build:**

- **Dark mode:** the game screen stays light on purpose (D24).
- **Fonts:** Alfa Slab One (OFL, `assets/fonts/`) is bundled and loaded by the game
  screen only, so it adds nothing to app startup. Mono text uses the app's SpaceMono;
  body text uses the system font through `AppText`. Card faces use plain `Text` so they
  keep their printed-card proportions regardless of the text-size setting.
- **Gestures:** Pressable taps and long-press only; `react-native-gesture-handler` isn't
  installed and drag-to-play is out of scope.

**Not yet checked on a device:** everything above was verified by tests and in Expo web.
On-device checks still owed: the cost-circle digit centering (the face draws digits
slightly high; there's a small nudge), the safe-area insets, and long-press timing.

## 14. Risks and open questions

- **Who is the opponent?** Head-to-head with a friend, vs AI, or async online. This drives
  the engine's networking and whether an AI is a launch requirement.
- **Physical, digital, or both?** A print-on-demand deck is a legitimate product on its own
  and the cheapest test. Digital removes the bookkeeping (level, bank, down and distance).
- **Monetization trap: random card packs are loot boxes.** Randomized packs bring app
  store odds-disclosure requirements and possible gambling regulation in some
  jurisdictions. A fixed deck is cleaner. Deckbuilding (choosing your cards from a pool)
  can come later as a mode.
- **No dice means less comeback variance.** The weaker player wins less often -- good for
  competitive play, worse for casual. The shuffle and Power Plays are the swing sources.
- **Analysis paralysis.** Printed yardage invites counting. Keep the defense's hand hidden;
  consider a soft turn timer in digital play.
- **Solvability.** A small read space plus deterministic yardage could converge on a
  dominant pattern. Read-blurring cards are the counter-design.
- **Defense agency.** Defensive stamina and Power Plays are meant to make defense active.
  Confirm it in playtesting.

## 15. Next steps

1. **Play the app build on a device** (dev client or an internal build) and fix what the
   web check couldn't show (section 13).
2. **Ship** with the next EAS update (the font is a JS asset, so an OTA update carries it).
3. **Balance through play** -- yardage curve, draw rates (D17), and the uncountered cards
   (Blocked Kick, Kick Six).
4. **Art:** card images by id per section 11.

## Appendix: the dice model (retired 2026-10-19)

The first draft resolved each snap with 2d6:

- Offense and defense picked from 4 x 4 fixed, reusable calls.
- A matchup table gave a +1/0/-1 tier, and the tier shifted the roll on a per-play yardage
  table.
- Turnovers happened only on a natural 2 -- an early version that let modifiers push rolls
  into the turnover slot hit a 32% turnover rate.
- A separate Momentum currency, earned from first downs and stops, paid for power cards.

Tuned, it reached about 2.95 points per drive over 8.6 snaps.

It was retired because dice put outcomes in the roll instead of the cards. The stamina
model replaces both the roll and the Momentum currency with one resource. The fixed-start
format, no punting, timing windows, card categories, and seed-card intent all carried
over.
