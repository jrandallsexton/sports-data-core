# SmackBot: an opt-in voice for pick-result notifications

**Status**: Notification-side engine (ships dark) ·
**Origin**: the original sportDeets premise — talking smack

## Why

Pick-result pushes currently read in one neutral voice:

> **Nice pick!** / **Tough loss**
> Sluggers: BOS 3, NYY 2 — you picked BOS ✓

Correct, informative, forgettable. The product's founding premise was
tormenting your friends over bad picks, and a notification that *taunts* the
user is an engagement mechanic in a way a scoreline never is. Anger brings
people back — "I'll show them" is a stronger return signal than satisfaction.

SmackBot is an **opt-in voice** for the same event. Same trigger, same
targeting, same dedupe; only the copy changes.

## Where the phrases live

**PostgreSQL, in Notification's own database — never in this repo.**

This mirrors the `Prompt` entity's rationale verbatim: *"The database is
private (the repo is public), so text lives here."* Two reasons it matters
more here than for prompts:

1. The voice IS the differentiator. A public phrase list is copyable in an
   afternoon.
2. Users reading every taunt in advance kills the surprise that makes it
   work.

Notification's own database (rather than API's, where `Prompt` lives) because
pick-result pushes fire in bursts as a slate finalizes — a cross-service call
per notification, for content that changes weekly at most, would be waste. A
local table needs no projection to keep in sync and no cache invalidation.

**This document deliberately contains no phrase text.** Lines are inserted by
SQL kept outside source control.

## Schema

`SmackPhrase`, modelled on `Prompt` with one deliberate divergence.

| Column | Purpose |
|---|---|
| `Voice` | `Standard` \| `Smack` — an enum, not a bool, so later voices (hype-man, deadpan analyst) need no schema change |
| `Situation` | the resolution slot (taxonomy below) |
| `Sport?` | null = any sport; a sport-specific row outranks it, same precedence rule as `Prompt` |
| `Text` | the line, with `{...}` tokens |
| `IsActive` | soft on/off without deleting history |
| `RequiresGamblingContent` | gates spread-flavoured lines (see below) |
| `Weight` | relative selection frequency |
| `Description` | operator note |
| `RowVersion` | `xmin`, for the eventual management UI |

**The divergence**: `Prompt` resolves to exactly ONE `IsDefault` row per slot,
enforced by partial unique indexes. SmackPhrase wants MANY active rows per
slot with one chosen at send time, so `IsActive` replaces `IsDefault` and the
unique-slot indexes do not carry over.

## Situation taxonomy

Derived entirely from the existing `UserPickScored` fat event — no new
lookups. Thresholds live in one place in `PickSituationResolver`.

| Situation | Condition |
|---|---|
| `ShutoutLoss` | picked side scored 0 and lost |
| `BlowoutLoss` | lost by ≥ 21 |
| `BigDogLoss` | picked an underdog of ≥ 10 and lost |
| `FavoriteChoked` | picked a favourite of ≥ 10 and lost |
| `SqueakerLoss` | lost by ≤ 3 |
| `NarrowMissAts` | ATS: missed the cover by ≤ 1 |
| `WonButDidNotCover` | ATS: picked side won the game but missed the cover |
| `GenericLoss` | any other loss |
| `DogWin` | picked an underdog of ≥ 10 and won |
| `ChalkWin` | picked a favourite of ≥ 14 and won |
| `BlowoutWin` | won by ≥ 21 |
| `UglyWin` | won by ≤ 3 |
| `CoveredInDefeat` | ATS: the pick cashed although the picked side lost |
| `GenericWin` | any other win |

Resolution is **most-specific-first**; the generic buckets guarantee every
scored pick maps to something, so the engine can never fail to produce copy.

### Pick outcome is not scoreboard outcome

`IsCorrect` means the pick **covered**, not that the picked side won, and in an
ATS league those diverge. A +14 dog losing 24-20 cashes; a -7 favourite winning
by 3 does not. Every win bucket below is phrased around victory and every loss
bucket around defeat, so a margin whose sign contradicts the pick outcome is
diverted to `CoveredInDefeat` or `WonButDidNotCover` before any of them are
reached. Without that, copy would congratulate a team that lost or console one
that won.

### The straight-up spread gap

The flagship case — *"took a 14-point dog straight up and lost"* — needs the
spread on a **StraightUp** pick, and the event doesn't carry it:
`PickScoringProcessor` populates `PickedSpread` only when
`group.PickType == AgainstTheSpread`. The value exists on the matchup result
regardless; the gate is about display semantics (don't render a spread in
straight-up copy), not availability.

Fix is a new nullable `MarketSpread` on the event, populated whenever known,
leaving `PickedSpread` untouched so existing copy doesn't change. **Until
that lands, the spread-dependent situations simply don't match** and those
picks fall through to the generic buckets — degraded, never broken.

## Voice selection

`UserNotificationPreferences.PickResultVoice`, defaulting to `Standard`.
Everyone stays on the current copy until they opt in, so this ships dark.

`PickResultEnabled` still gates delivery entirely — voice only decides how a
notification that IS being sent reads.

## Phrase selection is deterministic

The chosen line is seeded from `PickId`. Candidates are ordered by id (the
database guarantees no ordering), each is assigned a slice of the hash space
proportional to its `Weight`, and `hash(PickId) % totalWeight` selects the
slice. No duplicates are materialized and the modulo is over the summed
weight, not the candidate count. Stable under redelivery, reproducible in
tests, and still varied across picks and users — no RNG to inject or mock.

## Gambling-content interaction

Spread-referencing taunts are betting content. A user in an **ATS** league has
opted into spread-based scoring, so referencing the line is fair. A user in a
**StraightUp** league who has hidden gambling content has not, and must not
receive "Vegas said 14, you said nah."

`RequiresGamblingContent` marks those rows; the catalog filters them out when
the context doesn't permit them.

**Operator content requirement (not enforced in code):** every situation should
carry at least one non-spread line, otherwise the filter empties that bucket
for straight-up players and they silently receive standard copy. The catalog
treats an empty bucket as a supported fallback, so nothing breaks — the cost is
a missing taunt, not an error. Worth a validation query once the library is
populated.

## Safety rails

Opt-in is the load-bearing one: the user chose this. Beyond that, the editorial
contract for anyone adding lines:

- Mock the **pick**, never the person. No appearance, intelligence, or
  identity.
- Nothing touching protected characteristics, ever.
- No profanity — app-store review reads notification copy.
- No streak-piling ("you're 0-for-your-life"). Compounding failure tips from
  funny into discouraging, which inverts the intended effect.
- Wins get **grudging** credit, not praise. A voice that turns nice on wins
  isn't a voice, it's a participation trophy.

## History is immutable for free

`NotificationUserPick` already stores `Title` and `Body` per send, so the exact
line a user received is captured. Editing a phrase later never rewrites
history — the same guarantee `Prompt` captures provide.

That capture is also a feedback loop: joining sent phrases against subsequent
user activity shows which taunts actually drive return visits and which merely
annoy, turning the library from guesswork into something tunable.

## Sequencing

1. **This change** — schema, situation resolver, catalog, wired into
   `UserPickScoredConsumer` behind a preference that is always `Standard`.
   Ships dark.
2. API — `MarketSpread` on the event; `PickResultVoice` through
   entity/DTO/command/query + migration, projected into Notification.
3. Mobile — the settings toggle that lets a user turn it on.

Phrase rows are inserted out-of-band at any point; an empty catalog falls back
to the standard copy.
