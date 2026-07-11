# Notification preferences

Per-category push-notification opt-out. A user can turn off any of eight
notification categories from **Profile → Notifications**. Defaults are
everything-on: this is a pick'em product where the notifications *are* the
engagement layer, so opting out is the rare path.

## Ownership & data flow

The **API is the canonical owner**. The **Notification service** keeps a local
projection that its dispatchers read to gate sends. They stay in sync over an
event — the API never calls the Notification service directly.

```
Mobile settings screen
  GET  /user/me/notification-preferences   → current flags (all-on if never set)
  PATCH /user/me/notification-preferences  → full replacement of all 8 flags
        │
        ▼
API: UpdateNotificationPreferencesCommandHandler
  - upserts canonical UserNotificationPreferences row (one per user)
  - publishes UserNotificationPreferencesUpdated (EF outbox, atomic with the write)
        │
        ▼ (MassTransit)
Notification: UserNotificationPreferencesUpdatedConsumer
  - upserts the local UserNotificationPreferences projection (idempotent by UserId)
        │
        ▼
Notification dispatchers / schedulers / consumers
  - read the projection; a "false" flag suppresses that category
```

### Why event projection, not a service-to-service call

Every other cross-service user fact (User, UserDevice, UserPick, membership)
already flows API → Notification as an event-sourced projection. Preferences
follow the same pattern rather than adding a synchronous
Notification-reads-from-API (or API-reads-from-Notification) hop. The
Notification service stays authoritative for *dispatch* while the API stays
authoritative for *intent*.

## The eight categories

| UI label                    | Flag                          | Enforced by |
|-----------------------------|-------------------------------|-------------|
| Pick results                | `PickResultEnabled`           | `UserPickScoredConsumer` |
| Pick deadline reminders     | `PickDeadlineReminderEnabled` | `NotificationDispatcher`, `PickDeadlineReminderScheduler` |
| Kickoff reminders           | `ContestStartReminderEnabled` | `NotificationDispatcher`, `ContestStartReminderScheduler` |
| League invites              | `LeagueInviteEnabled`         | `UserInvitedToPickemGroupConsumer` |
| League membership updates   | `MembershipEnabled`           | `PickemGroupMemberAddedConsumer` |
| Matchup previews            | `MatchupPreviewEnabled`       | *projected only — not yet gated* |
| Schedule changes            | `ScheduleChangeEnabled`       | *projected only — not yet gated* |
| Line moves                  | `OddsChangedEnabled`          | `ContestOddsUpdatedConsumer` |

Six categories are actively gated in the dispatch path today. **Matchup previews**
and **schedule changes** are projected and stored but no dispatcher consults them
yet — the toggles are exposed now (forward-looking) and go live the moment those
notification types start honoring the flag. Flip the switch in the corresponding
consumer with the same `prefs is { XEnabled: false }` guard the others use; no
schema or contract change is needed.

## Absent-row semantics

A user with **no** preferences row is treated as **all-enabled**. The row is
created lazily on the first PATCH (first time the user changes any setting):

- **API GET** projects all-true defaults when the row is missing (never 404s).
- **Dispatchers** use the `prefs is { XEnabled: false }` pattern, so a null
  projection (`prefs == null`) never suppresses anything.

This keeps the tables small — only users who have actually opted something out
carry a row on either side.

## Idempotency

- The **event carries the full flag set**, so the consumer is a straight upsert
  by `UserId`. At-least-once redelivery and republish-on-every-change both
  converge on the same row.
- Race-safe insert: two concurrent "doesn't exist" inserts let the unique-index
  loser (Postgres `23505`) fall through to the update path — same pattern as
  `UserDataPublishedConsumer`.
- Account deletion purges the projection via `UserDeletedConsumer` (already
  wired; preferences were in its purge set before this feature shipped).

## Mobile

- `usersApi.getNotificationPreferences()` / `updateNotificationPreferences(prefs)`
  wrap the two endpoints.
- `app/settings/notifications.tsx` holds the full flag set in local state and
  PATCHes the whole set on each toggle (optimistic, with revert on failure).
  Reached from the "Notifications" row in `app/(tabs)/profile.tsx`.
