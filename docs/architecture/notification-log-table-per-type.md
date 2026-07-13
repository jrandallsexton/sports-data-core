# Notification logging: table-per-type

**Status:** Approved. **PR1 (PickResult → `NotificationUserPick`) implemented in
the PR that introduces this doc.** PRs 2–5 (see "Migration & rollout") are
planned and not yet started.
**Owner:** Randall.
**Scope:** `SportsData.Notification` service (+ one additive field on a `SportsData.Core` event).

## Motivation

Today every notification the service emits — six distinct kinds — writes one row to a
single catch-all `NotificationLog` table. That table doubles as the idempotency
store via a unique index on `(CorrelationId, UserId, Channel)`.

The catch-all has two problems:

1. **Visibility (primary).** The row records *that* we sent something, but not
   *what it was about*. There is no `PickId`, `ContestId`, or `LeagueId` — just a
   free-text `Category` discriminator and a `CorrelationId`. Answering "did we
   notify this user about this pick?" means correlating opaque guids across
   services. A per-type table with typed, `NOT NULL` metadata columns makes the
   log directly queryable and self-documenting.

2. **Duplicate detection (secondary).** Users have received duplicate pushes for
   some pick results. Root cause below. Typed tables let each notification kind
   enforce its *natural* dedup key as a plain `UNIQUE` index instead of leaning
   on a transport correlation id.

Splitting is chosen for **visibility first**; cleaner dedup is a welcome side
benefit. This is a modeling improvement, **not** the only way to stop the
duplicates — see "Why the duplicates happen" — but it gives us an enforced,
legible place to fix them.

## Current state

`NotificationLog` (`Infrastructure/Data/Entities/NotificationLog.cs`) columns:
`Id, UserId, CorrelationId, Category, Channel, Title, Body, Result, FailureReason,
AttemptedUtc` + audit fields from `CanonicalEntityBase<Guid>`. Unique index:
`(CorrelationId, UserId, Channel)` (`AppDataContext.cs`).

Six categories write to it, via **two different dedup strategies**:

| Category      | Source                                              | CorrelationId source        |
|---------------|-----------------------------------------------------|-----------------------------|
| PickResult    | `UserPickScoredConsumer` ← `UserPickScored`         | **transport** (event's)     |
| LeagueInvite  | `UserInvitedToPickemGroupConsumer` ← `UserInvitedToPickemGroup` | **transport**    |
| Membership    | `PickemGroupMemberAddedConsumer` ← `PickemGroupMemberAdded`     | **transport**    |
| OddsChanged   | `ContestOddsUpdatedConsumer` ← `ContestOddsUpdated` (fan-out)   | **transport**    |
| PickDeadline  | `NotificationDispatcher.SendPickDeadlineReminderAsync`         | **derived** (deterministic) |
| ContestStart  | `NotificationDispatcher.SendContestStartReminderAsync`        | **derived** (deterministic) |

### Why the duplicates happen

The two reminder paths derive `CorrelationId` from
`DeterministicCorrelationId(category, userId, scopeId, qualifier…)` — a stable
hash of the notification's *semantic identity*. A re-fire produces the same guid,
collides on the unique index, and is suppressed. **These are idempotent by
construction and do not duplicate.**

The event-driven paths use the **transport** `CorrelationId` carried on the
inbound event. That is the leak, in *both* directions for `PickResult`:

- **Under-suppression (duplicates).** The same logical "your pick resolved" can
  arrive from two correlation chains — the `ContestFinalized` event *and* the
  cron finalization backstop — with two different CorrelationIds. Neither
  collides with the other → two pushes.
- **Over-suppression (dropped notifications).** Within a single scoring run,
  `PickScoringProcessor` publishes one `UserPickScored` per pick. A user in three
  leagues who picked the same game gets three events **sharing one**
  `command.CorrelationId`. The current index collapses them to **one** push,
  silently dropping the other two league notifications.

The transport CorrelationId is simply the wrong dedup key for these events. The
right key is the notification's semantic identity (for a pick result: the pick).

## Goals / non-goals

**Goals**
- One typed table per notification kind, each with rich `NOT NULL` metadata.
- Each table enforces its own natural dedup key as a `UNIQUE` index.
- Preserve the existing atomic-claim idempotency pattern (claim row first, then
  dispatch, then finalize) — it moves from `NotificationLog` to the typed table.
- Keep `NotificationLog` in place, frozen, until it is inspected and dropped in a
  later PR. **This doc does not remove it.**

**Non-goals**
- The notification **fan-out policy** (do we actually send three pushes when a
  user picked one game in three leagues, or coalesce to one "your picks
  resolved"?) is explicitly **out of scope** and deferred. The schema is designed
  so this stays a send-side decision, not a re-model (see PickResult).
- No change to the two reminder paths' behavior — they already dedupe correctly;
  they just move to typed tables.
- No user-facing surface. These tables remain audit/debug/idempotency stores,
  never read by the app.

## Design principles

1. **Independent tables, not inheritance.** No EF TPT/TPH, no shared base
   *entity*. Each table is self-contained and carries its own copy of the common
   columns. Cost: a cross-type "everything we sent user X" query needs a `UNION`
   — acceptable for an audit/debug store that has no hot-path reader.
2. **Shared column *shape*, documented once.** Every typed table carries the
   common columns below plus its typed metadata. Consistency by convention, not
   by a base class.
3. **Capture more IDs than the dedup key needs.** Store every identifier
   available at creation time even if the current dedup key ignores it. This is
   the whole point (visibility) and it keeps future dedup/rollup changes to an
   index/query change rather than a migration + event change.

### Common column shape (every typed table)

From `CanonicalEntityBase<Guid>`: `Id, CreatedUtc, ModifiedUtc, CreatedBy,
ModifiedBy`. Plus, on every notification table:

| Column         | Type          | Notes                                                        |
|----------------|---------------|--------------------------------------------------------------|
| `UserId`       | `Guid`        | Recipient. Required.                                         |
| `Channel`      | `varchar(16)` | `"Fcm"` today; reserved for `Email`/`Sms`.                  |
| `Title`        | `varchar(256)`| Rendered push title (nullable until dispatched).            |
| `Body`         | `varchar(1024)`| Rendered push body (nullable until dispatched).            |
| `Result`       | `varchar(64)` | `Dispatching`→`Sent`/`Suppressed_*`/`Failed_*`. Required.   |
| `FailureReason`| `varchar(512)`| Truncated per-device summary; nullable.                     |
| `AttemptedUtc` | `timestamptz` | Claim time.                                                 |
| `CorrelationId`| `Guid`        | Retained for cross-service tracing, **not** the dedup key.  |

The typed dedup key replaces `(CorrelationId, UserId, Channel)` — `CorrelationId`
stays as a column for tracing but no longer participates in uniqueness.

## The six tables

Naming follows the pattern the two named examples set (`NotificationUserPick`,
`NotificationLeagueInvitation`): `Notification` + subject.

### 1. `NotificationUserPick` — PickResult

Source: `UserPickScored`. **Requires an event change** — add `PickId` (see below).

| Metadata column | Type   | Source                        |
|-----------------|--------|-------------------------------|
| `PickId`        | `Guid` | **new** on `UserPickScored`   |
| `ContestId`     | `Guid` | `UserPickScored.ContestId`    |
| `LeagueId`      | `Guid` | `UserPickScored.LeagueId`     |

**Dedup index:** `UNIQUE (UserId, PickId)` — the correctness floor. A pick is
scored once; this guarantees at most one push per pick and stops the cross-run
duplicate. It also *preserves* legitimate per-league notifications (three picks =
three distinct `PickId`s), which the current key wrongly drops.

> **Open — fan-out policy (deferred).** `UNIQUE (UserId, PickId)` permits three
> pushes for a user who picked one game in three leagues. Whether we *want* that
> or should coalesce to one rolled-up notification is a **send-side** decision to
> make later. Because the table stores `PickId`, `ContestId`, and `LeagueId`, a
> future rollup is a query/dispatch change ("have I sent for any pick in this
> contest?"), not a schema change. We are **not** deciding this now.

### 2. `NotificationLeagueInvitation` — LeagueInvite

Source: `UserInvitedToPickemGroup` (`InviteeUserId`, `GroupId`, `InvitedByUserId`).

| Metadata column   | Type   | Source                                |
|-------------------|--------|---------------------------------------|
| `LeagueId`        | `Guid` | `GroupId`                             |
| `InvitedByUserId` | `Guid` | `InvitedByUserId`                     |

**Dedup index:** `UNIQUE (UserId, LeagueId, CorrelationId)` — **re-invites
re-notify** (decided: the user may have missed the first, so send again). Each
invite *action* has its own `CorrelationId` (stable across at-least-once
redelivery, distinct across separate invites), so this is the one event-driven
category where correlation-level dedup is *correct*: redelivery of one invite
collides and is suppressed; a genuine second invite has a new `CorrelationId`
and sends. If an explicit `InvitationId` is ever added to the event, prefer it
over `CorrelationId` in the key.

### 3. `NotificationMembership` — Membership

Source: `PickemGroupMemberAdded` (`UserId`, `GroupId` — minimal payload; the
consumer docstring already flags this event as under-fattened).

| Metadata column | Type   | Source    |
|-----------------|--------|-----------|
| `LeagueId`      | `Guid` | `GroupId` |

**Dedup index:** `UNIQUE (UserId, LeagueId)`.

> Note: this event is the poorest in context. Fattening it (LeagueName,
> CommissionerUserId) is tracked separately and is **not** a prerequisite here —
> the dedup key only needs `(UserId, GroupId)`.

### 4. `NotificationOddsChange` — OddsChanged

Source: `ContestOddsUpdated` — **broadcast, fanned out per user** at dispatch
(consumer joins `UserPicks ⋈ PickemGroups`, filtered to the movement type). Event
carries `ContestId` only.

| Metadata column | Type   | Source      |
|-----------------|--------|-------------|
| `ContestId`     | `Guid` | `ContestId` |

> **Open — dedup grain.** Odds legitimately move multiple times for one contest;
> we *want* a notification per meaningful movement but not per redelivery of the
> same movement. `UNIQUE (UserId, ContestId)` is too coarse (suppresses the 2nd
> real move). Options: keep a per-movement qualifier column (e.g. an odds
> snapshot id or the event's CorrelationId) in the unique key, i.e.
> `UNIQUE (UserId, ContestId, MovementKey)`. Needs a decision during
> implementation; this is the one category where the current correlation-level
> dedup may be *intentional*.

### 5. `NotificationPickDeadline` — PickDeadline (reminder)

Source: `NotificationDispatcher.SendPickDeadlineReminderAsync(userId,
pickemGroupId, seasonWeek, fireTimeUtc)`. Already deterministic.

| Metadata column | Type   | Source           |
|-----------------|--------|------------------|
| `LeagueId`      | `Guid` | `pickemGroupId`  |
| `SeasonWeek`    | `int`  | `seasonWeek`     |
| `FireTimeUtc`   | `timestamptz` | `fireTimeUtc` (versioning) |

**Dedup index:** `UNIQUE (UserId, LeagueId, SeasonWeek, FireTimeUtc)`. The
`FireTimeUtc` component preserves today's fire-time versioning (a rescheduled
reminder re-fires; a Hangfire retry of the same fire does not).

### 6. `NotificationContestStart` — ContestStart (reminder)

Source: `NotificationDispatcher.SendContestStartReminderAsync(userId, contestId,
fireTimeUtc)`. Already deterministic.

| Metadata column | Type          | Source        |
|-----------------|---------------|---------------|
| `ContestId`     | `Guid`        | `contestId`   |
| `FireTimeUtc`   | `timestamptz` | `fireTimeUtc` |

**Dedup index:** `UNIQUE (UserId, ContestId, FireTimeUtc)`.

## Event contract change

Only one event needs to change:

- **`UserPickScored`** (`SportsData.Core/Eventing/Events/Picks/UserPickScored.cs`)
  — add `Guid PickId`. `PickScoringProcessor` already has the pick in hand
  (`pick.Id`) inside the publish loop, so populating it is a one-line change.

  *Deploy consideration:* it is a positional record, so adding the field is a
  compile-time change to the single publisher. In-flight messages queued before
  deploy would deserialize `PickId` as `Guid.Empty`, and because `PickId` is the
  dedup key, two such messages for one user could false-collide. **In the current
  environment this is moot** — MLB has a single user, yesterday's picks are all
  scored and notified, and the pick-scored queue is empty. Ship the additive
  field as-is; no drain or `Empty` guard required. (Revisit if this lands during
  an active multi-user scoring window before a football season.)

No other event changes — LeagueInvite, Membership, OddsChanged, and both
reminders already carry (or derive) everything their dedup key needs.

## Migration & rollout

`NotificationLog` **stays** — frozen. Each consumer is cut over to write **only**
its typed table (the atomic claim + audit row moves there). `NotificationLog`
simply stops receiving new rows and remains for historical inspection until a
later PR drops it. No dual-write: a given consumer claims in exactly one table,
so cutover is clean per consumer and idempotency is never split across two
indexes.

**Sequencing** (all-in overall, phased across PRs to keep each reviewable):

1. **PR 1 — PickResult.** Add `PickId` to `UserPickScored`; add
   `NotificationUserPick` entity + migration + unique index; cut
   `UserPickScoredConsumer` over; tests. *This carries the duplicate fix — highest
   value, do first.*
2. **PR 2 — LeagueInvitation + Membership.** Both are `(UserId, LeagueId)`;
   natural to land together.
3. **PR 3 — ContestStart + PickDeadline.** Reminders; move the deterministic
   claim into the typed tables (behavior-preserving).
4. **PR 4 — OddsChange.** Requires the movement-grain decision above; lowest
   urgency, most design.
5. **PR 5 (later, separate) — retire `NotificationLog`** once you've finished
   picking through it: drop the table + entity + index.

Each migration is additive (new table + indexes only) and independently
reversible. **Migrations validated locally before each PR** per project guardrail
(`memory/feedback_test_migrations_locally`).

## Testing

Per typed table (mirror `UserPickScoredConsumerTests` / the dispatcher tests):

- Claim inserts a `Dispatching` row with all metadata populated.
- Redelivery with the same natural key hits the unique constraint → the
  "already claimed" branch → no duplicate dispatch.
- **PickResult specifically:** three picks (same user, same contest, three
  leagues) each produce a distinct row and three dispatches (proving the
  over-suppression bug is fixed); a second scoring run of the same pick (distinct
  transport CorrelationId) collides and is suppressed (proving the
  under-suppression/duplicate bug is fixed).
- Suppression paths (`Suppressed_UserOptedOut`, `Suppressed_NoDevice`) still
  finalize the typed row.

## Risks / tradeoffs

- **`UNION` for cross-type audit.** No hot-path reader, acceptable.
- **Six migrations + six near-identical entities.** Boilerplate, but each is
  small and self-contained; independence is the explicit design choice.
- **`UserPickScored` field add** during deploy overlap — mitigated above.
- **OddsChange movement grain** is a genuine open question, isolated to PR 4.

## Open decisions to confirm before / during implementation

1. **PickResult fan-out policy** — deferred; schema is built to not force it now.
2. **OddsChange dedup grain** — per-movement qualifier in the unique key vs.
   correlation-level; decide in PR 4.
3. ~~Re-invite re-notify~~ — **decided: re-notify.** `NotificationLeagueInvitation`
   keys on `(UserId, LeagueId, CorrelationId)`. (Membership stays `(UserId,
   LeagueId)` — a re-add is rare; revisit only if it becomes a real case.)
4. ~~Deploy strategy for the `UserPickScored` field add~~ — **moot.** Single MLB
   user, empty pick-scored queue; ship the additive field as-is.
