# Notification service — events and state model

Foundational architecture doc for `SportsData.Notification`. Derives the
service's event consumption and projection state by working backwards
from the notifications it needs to send. Pairs with
`docs/mobile/notifications-and-live-updates.md` (the broader scoping
plan) — that doc covers transport, mobile UX, and phased delivery; this
doc covers the data architecture inside Notification.

> **Status (2026-06-23)**: pre-implementation. Phase 1a (pod + broker
> standing in cluster, idle) is live; this doc grounds the Phase 1b
> (shovels) and Phase 3 (event fan-out) decisions ahead.

## 1. Why work backwards from notifications

The first instinct on "what events does Notification need?" is to
enumerate every domain event in the system and bridge them all in. That
produces dead infrastructure — shovels carrying events nothing
consumes, projection tables nobody reads. The right starting question is
"what notifications do we send?" — every answer derives from that.

## 2. Notification catalog (v1 + planned)

| # | Notification | Trigger type | Trigger event / job | Data the body needs |
|---|---|---|---|---|
| 1 | "BOS @ SEA is final. Your pick was correct" | event | `UserPickScored` (per user × contest × league) | UserId, FCM token, team names, scores, pick value, IsCorrect, league name |
| 2 | "Picks for League X are due in 1 hour" | scheduled | Hangfire recurring job | User, league name, list of unsubmitted contests, deadline time |
| 3 | "BOS @ SEA starts in 15 min" | scheduled | Hangfire recurring job | User, contest, team names, start time |
| 4 | "Randall invited you to League X" | event | `LeagueInviteCreated` | Invitee, inviter name, league name |
| 5 | "Welcome to League X" / "Y joined your league" | event | `PickemGroupMembershipCreated` | League name, joined-user name, optionally existing members |
| 6 | "Preview ready for BOS @ SEA" | event | `MatchupPreviewGenerated` | Commissioner, contest |

**Important upstream nuance**: the pick-result notification (#1) is
**not** triggered by `ContestFinalized`. Producer fires
`ContestFinalized`; API consumes it and scores picks; **API** fires
`UserPickScored` carrying the per-user outcome. Producer doesn't know
about user picks.

## 3. Two strategies for getting Notification the data it needs

Notification cannot read API's or Producer's databases. Everything
arrives via events. Two approaches:

| Approach | What it costs | What it buys |
|---|---|---|
| **Fat events** — publisher denormalizes everything the consumer needs into the event payload | Larger payloads, schema coupling between publisher and consumer | Notification needs almost no projections; consumer logic is "read event, send notification" |
| **Thin events + full projection** — Notification consumes CRUD-style events and maintains its own projections of User, League, Membership, Contest, Pick | 10+ projection tables, ordering and cold-start complexity | Notification owns its read model fully and independently |

### Decision: fat events

Reasons:

- Notification's value comes from sending good notifications, not from
  maintaining a perfect mirror of the rest of the platform's state.
- The set of *facts* needed at notification-send time is small and
  per-event already; pushing those facts into the event eliminates ~6
  projection tables and ~12 CRUD-style events that would otherwise be
  needed to feed them.
- The cost of fat events (schema coupling) is real but bounded: each
  notification-driving event is a contract between exactly one
  publisher (API or Producer) and one primary consumer (Notification).
  We already manage that kind of coupling for every other event in the
  system.
- We can always *fatten* an existing thin event by adding fields; the
  inverse (projecting from thin events) is much harder to walk back.

### What Notification still projects locally

State that **must** live in Notification because no upstream owns it,
or because it needs to be queryable for scheduled jobs:

| Table | Purpose | Source |
|---|---|---|
| `UserDevice` | FCM tokens per user, platform, master `NotificationsEnabled` | Own API: mobile POSTs `/user/devices` at sign-in after permission grant |
| `UserNotificationPreferences` | Per-user, per-category opt-in/out | Own API: Profile > Notifications screen |
| `PendingScheduledJob` | Hangfire job ids for deadline + kickoff reminders per (UserId, ContestId) or (UserId, LeagueId, Week) | Derived from `PickemGroupWeekCreated` + `ContestCreated` events on receive |
| `NotificationLog` | What was sent, when, to whom, on which channel, with what result | Own — write-only audit table |

Four tables. Notification does **not** project User, League,
Membership, Contest, or Pick. Those facts ride on the events.

## 4. Event taxonomy

Every event below is a fat-event candidate. The "fat payload" column
lists the fields Notification needs in the payload. Producer / API may
already publish a thinner version — getting these events to the right
shape is a prerequisite work item per event (see §7 Audit gaps).

| Event | Source broker | Fat payload Notification needs | Drives notification(s) |
|---|---|---|---|
| `UserPickScored` | API | `UserId`, `DisplayName`, `ContestId`, `AwayName`, `HomeName`, `AwayScore`, `HomeScore`, `PickValue`, `IsCorrect`, `LeagueId`, `LeagueName` | #1 pick result |
| `LeagueInviteCreated` | API | `InviteeUserId` or `InviteeEmail`, `InviterUserId`, `InviterDisplayName`, `LeagueId`, `LeagueName` | #4 invitation |
| `PickemGroupMembershipCreated` | API | `JoinedUserId`, `JoinedDisplayName`, `LeagueId`, `LeagueName`, `CommissionerUserId` | #5 welcome + commissioner-side notification |
| `MatchupPreviewGenerated` | API | `CommissionerUserId`, `ContestId`, `AwayName`, `HomeName` | #6 preview ready |
| `PickemGroupWeekCreated` | API | `LeagueId`, `LeagueName`, `Week`, `SeasonYear`, `PickDeadlineUtc`, `ContestIds[]` | #2 deadline reminder (schedules Hangfire job; doesn't directly send) |
| `ContestCreated` | per-sport Producer (NCAA / NFL / MLB) | `ContestId`, `Sport`, `AwayName`, `HomeName`, `StartDateUtc` | #3 kickoff reminder (schedules Hangfire job) |
| `ContestStartTimeUpdated` | per-sport Producer | `ContestId`, new `StartDateUtc` | Reschedules existing kickoff-reminder job |
| `ContestCancelled` | per-sport Producer | `ContestId`, `AwayName`, `HomeName` | Cancels scheduled jobs + optional "game cancelled" notification |

**5 events from the API broker, 3 events per Producer broker.** With
three Producer brokers active (NCAA, NFL, MLB), the per-sport-Producer
events expand to 9 distinct shovel YAMLs (3 events × 3 sources). Plus
5 from API = **14 shovel YAMLs total** to fully cover the v1+ catalog.

## 5. Phased shovel rollout

Don't wire everything at once. Each version maps directly to a phase in
the broader notification rollout (`docs/mobile/notifications-and-live-updates.md`
§4).

### v1 — Pick-result notifications only

**1 shovel.** API → Notification, carrying `UserPickScored`.

Notification's first deliverable. Pick result is the highest-value
notification because every user who picked gets one and it ties
directly to game outcomes. Proves the end-to-end pipeline from
publisher → shovel → Notification consumer → FCM push.

### v2 — Scheduled reminders (deadlines + kickoffs)

**+7 shovels.** API → Notification for `PickemGroupWeekCreated`, plus
per-sport Producer → Notification for `ContestCreated` and
`ContestStartTimeUpdated` (×3 sports = 6).

Triggers the Hangfire-job-creation paths. The reminders themselves
fire from local Hangfire, not from broker events at send-time.

### v3 — Social / membership / commissioner

**+3 shovels.** API → Notification for `LeagueInviteCreated`,
`PickemGroupMembershipCreated`, `MatchupPreviewGenerated`.

### v4 — Cancellations + schedule changes

**+3 shovels.** Per-sport Producer → Notification for
`ContestCancelled`.

### Cumulative shovel count

| Version | Shovel count | New |
|---|---|---|
| v1 | 1 | 1 |
| v2 | 8 | +7 |
| v3 | 11 | +3 |
| v4 | 14 | +3 |

## 6. Out-of-band requirements per shovel batch

Each shovel batch requires the same two operational steps that the
existing per-sport API shovels needed (see
`app/base/rabbitmq/shovels/README.md` in `sports-data-config`):

1. **Credentials secret** — one per (source broker, destination
   broker) pair. The Notification broker rollout adds at most three new
   secret names: `shovel-baseball-mlb-to-notification-credentials`,
   `shovel-football-ncaa-to-notification-credentials`,
   `shovel-football-nfl-to-notification-credentials`, plus
   `shovel-api-to-notification-credentials`. Each created once via the
   `kubectl create secret generic ...` recipe in the README.
2. **Pre-declare destination exchanges on the Notification broker** —
   MassTransit only auto-declares the exchange on first publish, and
   nothing in Notification publishes to itself. Use `rabbitmqadmin
   declare exchange ...` on the Notification broker for each event
   exchange before the shovel reconciles, or messages publish to a
   non-existent exchange and silently drop.

## 7. Audit gaps — what doesn't exist yet

Before any shovel-wiring PR can ship, we need to verify which events
in §4 actually exist today and which need to be created or fattened.
Known gaps:

| Event | Status (suspected — needs verification before v1 PR) |
|---|---|
| `UserPickScored` | **Likely missing.** Picks are scored after `ContestFinalized` consumption in API, but a corresponding outbound event isn't (yet) on the wire. v1 cannot ship until this exists. |
| `ContestFinalized` | Exists. Fat shape may need additional team-name fields. |
| `ContestCreated` | Unclear — verify in `SportsData.Producer/Application/Contests`. |
| `ContestStartTimeUpdated` | Unclear — exists conceptually (Contest entity has `StartDateUtc`) but no dedicated event yet observed. |
| `ContestCancelled` | Likely missing. Cancellation is a recent capability; event surface may not be wired. |
| `LeagueInviteCreated` | Unclear — invites exist via the UI; verify in API. |
| `PickemGroupMembershipCreated` | Likely exists — membership flow is well-trodden. |
| `PickemGroupWeekCreated` | Likely exists. |
| `MatchupPreviewGenerated` | Likely exists (the picks page already chips it). |

The **first sub-ticket of any Phase 1b PR** should be an audit pass:
grep each event name across `SportsData.Core.Eventing.Events.*`, mark
each as "exists / exists but needs fattening / does not exist," and
gate the shovel-wiring on closing those gaps.

## 8. Hard problems worth naming explicitly

### 8.1 Cold start

First deploy of Notification with no prior event consumption — how does
it learn about existing users, leagues, contests? Options:

- (a) **Snapshot at cutover** — one-time dump from API/Producer at
  rollout. Clean line in the sand; predictable behavior. **Recommended.**
- (b) **Event replay from RabbitMQ** — requires durable storage; more
  infrastructure.
- (c) **Lazy-build** — only learn about users/leagues as new events
  arrive. Pre-existing entities don't get notifications until they next
  fire an event. Acceptable for a v1 with pick-result-only since
  everyone with an active pick will get a fresh `UserPickScored` event
  on every finalization.

For the fat-event approach, (c) is essentially free because Notification
doesn't project most state anyway. **Lock (c) for v1**; revisit if
projection needs grow.

### 8.2 Out-of-order delivery

RabbitMQ doesn't guarantee ordering across queues. If
`ContestStartTimeUpdated` arrives before `ContestCreated`, the
scheduled-job projection breaks. Fix: each event carries `OccurredUtc`;
consumer skips updates older than what's already in the
`PendingScheduledJob` row.

### 8.3 Late-arriving consumers

When a user joins a league mid-week, do they get the deadline reminder
for that week? Probably yes — but the `PickemGroupMembershipCreated`
event needs to trigger a "compute PendingScheduledJob rows for this
user × this league's open contests" cascade. Worth designing into the
membership-event handler from day one.

### 8.4 Fat-event payload size

A `UserPickScored` event fan-out for a popular contest could be
hundreds of events (one per user-pick-league combination). Acceptable
for now — RabbitMQ handles millions of small messages per second — but
worth knowing that pick-scoring becomes the largest event-publish wave
in the system on a Sunday afternoon.

### 8.5 Idempotency

Events may be redelivered (at-least-once delivery). The
`NotificationLog` table is the dedupe surface: before sending, check
whether `(EventId, UserId, Channel)` already exists in the log; skip if
so. The log doubles as audit trail and dedupe guard.

## 9. Related docs

- `docs/mobile/notifications-and-live-updates.md` — broader scoping
  plan: transport, mobile UX, four-phase delivery. This doc is its data
  architecture pair.
- `app/base/rabbitmq/shovels/README.md` (`sports-data-config`) —
  operational README for adding new shovels (credentials secrets,
  pre-declare gotcha, kubectl recipes).
- `memory/reference_per_sport_rabbitmq_split.md` — why per-sport
  brokers exist; never propose consolidating.
- `memory/reference_shovel_exchange_predeclare.md` — pre-declare
  gotcha context from the Round 1 Phase 4 migration.
- `memory/reference_cross_broker_shovel_audit.md` — debug methodology
  when "consumer registered but never fires."
