# Line-move notifications: name the game, link to it

**Status**: backend shipped; mobile pending device validation ·
**Origin**: tester feedback, 2026-08-16

## Problem

The line-move push read:

> **Line moved**
> The line moved on a game you picked: spread -3 → -1.5, total 36.5 → 37.5 (DraftKings).

Two defects, both visible in a single screenshot of a stacked tray:

1. **No game identity.** Nothing in the copy said *which* matchup moved, so
   two alerts an hour apart were indistinguishable.
2. **No destination.** Tapping opened the app's last screen; the user had to
   navigate to the game manually.

Root cause of (1) is structural, not editorial: `ContestOddsUpdated` carries
numbers and no team names, so no rewording could fix it.

## Decisions

**Enrich from Producer, don't widen the event.** `ContestOddsUpdated` stays a
thin fact ("odds moved, here are the numbers"). Notification fetches the
contest from Producer via `IContestClientFactory` → `GetContestById`. The
alternative — adding a name to the event — was cheaper in the moment (Producer
already has `Contest` loaded, so it was a free field) but worse in shape: it
bloats a contract three consumers compile against, forces a nullable-field
dance on every rolling deploy, and buys exactly one field. `SeasonContestDto`
already carries `ShortName`, `Name`, and `Week` — everything the copy and the
deep link need, with no contract change at all.

**`ShortName` needs no new abbreviation fields.** Producer already stores the
abbreviated form (`KC @ LV`, `IND @ NE`), which is what fits a notification
title.

**Enrich after the picker gate.** The consumer already returns early when
nobody picked the contest in an odds-sensitive league. The client call sits
after that, so only line moves that actually notify someone cost a request —
not every provider tick on every game.

**Degrade, never drop.** Any failure — unconfigured client slot, transport
error, non-success result — falls back to the original number-only copy and
still sends. Current behavior is the floor, so the worst case of adding this
dependency is what users already got.

## Copy

| | Before | After |
|---|---|---|
| Title | `Line moved` | `Line moved: KC @ LV` |
| Body | `The line moved on a game you picked: spread -3 → -1.5 …` | `Kansas City Chiefs at Las Vegas Raiders: spread -3 → -1.5 …` |

The matchup goes in the **title** because that is the boldest line in the tray
and the one that disambiguates stacked alerts; the full name in the body
disambiguates the abbreviation.

## Deep link

Payload follows the `kind`/id contract established by
`UserInvitedToPickemGroupConsumer`:

| key | value |
|---|---|
| `kind` | `OddsChanged` or `PickScored` |
| `target` | `matchup` |
| `contestId` | contest guid |
| `sport` | backend Sport enum name (`FootballNcaa`) |
| `leagueId` | the picked league (see below) |
| `week` | from `SeasonContestDto.Week`, when present |

Sport travels as the **enum name**, not route segments: the client maps it via
its own `resolveSportLeague`, keeping URL conventions client-owned. Mobile
routes through the existing `gameRoute()` helper, which already accepts
exactly `{sport, league, contestId, leagueId?, week?}`.

### Which league, when the user has several

A user routinely picks the same contest in more than one league. The target is
taken **from the already-filtered qualifying set** — the join that applies the
`PickType` filter — never from a separate "user's picks on this contest"
lookup. This matters: on 2026-08-16 production had 95 (user, contest) pairs
spanning multiple leagues, **57 of them spanning mixed pick types**. A naive
"oldest pick" lookup would have deep-linked into a StraightUp league — where a
line move is irrelevant — roughly 60% of the time. Within the qualifying set,
ordering is by the league's `CreatedUtc` (then id) so the choice is
deterministic across redelivery.

## Configuration (already provisioned)

Notification now calls `services.AddClients(config, mode)`. The contest client
resolves per sport, falling back to a mode-agnostic slot. **These keys already
existed** in Azure AppConfig under `Prod.All` (Notification runs `mode=All`,
and `AppConfiguration` selects `CommonConfig:*` at `{label}.{mode}`) — no
provisioning was needed:

```text
# label Prod.All (per-sport, what the factory resolves first)
CommonConfig:ContestClientConfig:FootballNcaa:ApiUrl → http://producer-svc-football-ncaa/api/
CommonConfig:ContestClientConfig:FootballNfl:ApiUrl  → http://producer-svc-football-nfl/api/
CommonConfig:ContestClientConfig:BaseballMlb:ApiUrl  → http://producer-svc-baseball-mlb/api/

# label Prod (mode-agnostic fallback)
CommonConfig:ContestClientConfig:ApiUrl              → http://producer-svc-football-ncaa/api/
```

Note the trailing `/api/` **with** the trailing slash: `HttpClient` relative
resolution only appends when the base ends in `/`, otherwise `contests/{id}`
would replace the last segment and drop the `/api` prefix. Producer's
`ContestController` has no auth filter, so the per-sport `SecretKey` entries
alongside these are unused on this path.

If a slot were ever absent the feature degrades silently to the old copy — the
notification still sends, so a missing key is a quality regression, not an
outage.

## Silent-gap diagnostic

When a contest has picks but no qualifying targets, the consumer now counts
picks whose `PickemGroup` projection is missing and logs a warning naming
`admin/backfill/pickemgroups`. This class of gap was real and invisible: on
2026-08-16, **13 of 16 AgainstTheSpread leagues had no local projection**, so
their members had never received a line-move notification and nothing was
logged. The inner join fails closed, which is right — but it should say so.

## Reuse: one contract, many kinds

The payload is built by `Application/Dispatching/MatchupDeepLink`, the
server-side twin of the client's `src/utils/deepLinks.ts`. Each side names the
contract once, so a new matchup-bound notification adds a kind constant on the
server and an entry in the client's `MATCHUP_KINDS` set — nothing else.

Kinds landing on the game page:

| Kind | Source | Notes |
|---|---|---|
| `OddsChanged` | `ContestOddsUpdatedConsumer` | week from `SeasonContestDto` (service call) |
| `PickScored` | `UserPickScoredConsumer` | week from the local `PickemGroupMatchup` projection — **no service call**; ids all ride on the event |

## Not in scope

Contest-start reminders have the same "nowhere to tap" gap and could reuse this
payload shape. Deferred to keep each change reviewable.
