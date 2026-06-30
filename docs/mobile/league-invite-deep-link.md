# League Invite Push + Deep-Link

Status: implemented (PR1 of the league-invite work)
Last updated: 2026-06-30

## Goal

GIVEN a user is already registered on sportDeets, WHEN someone invites that
user to a league, THEN the invitee receives a push notification whose tap
deep-links to a **league-invite preview** with a single **Join** CTA. No
auto-join — tapping a notification is not consent to join.

This is the first notification to actually populate the FCM `data` payload and
the first real deep-link tap handler (the broader plan lives in
`notification-deep-linking.md`; this note covers the invite case end to end).

## Scope (PR1)

Triggered off the **existing email-invite path**. The current
`SendLeagueInviteCommandHandler` sends a SendGrid template email and returns.
PR1 adds: if the invited email belongs to a **registered user who is not
already a member**, also publish a domain event so the Notification service can
push. Unregistered email → email only, unchanged. Inviting by username
(autocomplete) is a later PR and rides the same event.

## Wire contract

### Domain event (Core)

`SportsData.Core/Eventing/Events/PickemGroups/UserInvitedToPickemGroup`:

```csharp
record UserInvitedToPickemGroup(
    Guid InviteeUserId,
    Guid GroupId,
    string LeagueName,        // carried so the consumer needs no group lookup
    Guid InvitedByUserId,
    Sport Sport,
    int? SeasonYear,
    Guid CorrelationId,
    Guid CausationId) : EventBase(null, Sport, SeasonYear, CorrelationId, CausationId);
```

`LeagueName` rides on the event to keep the consumer free of a PickemGroup
projection read (and the race that comes with it).

### FCM `data` payload

```jsonc
{ "kind": "LeagueInvite", "target": "invite-preview", "leagueId": "<groupId>" }
```

FCM data values are strings. The picks page already resolves sport/league from
IDs, so the payload carries IDs only — no slug, no sport/league route strings.

## Backend

### API — publish on registered-non-member match

`SendLeagueInviteCommandHandler`, after the email send:

1. Look up `User` by `command.Email`.
2. If found AND not already a `PickemGroupMember` of the league, publish
   `UserInvitedToPickemGroup`.
3. Publish via `IMessageDeliveryScope.Use(DeliveryMode.Direct)` — the handler
   has no DbContext write, so it bypasses the MassTransit outbox (same pattern
   as `AdminController`). Unregistered / already-member → no event.

### Notification — consume and push

New `UserInvitedToPickemGroupConsumer`, mirroring `ContestOddsUpdatedConsumer`:

- Atomic `NotificationLog` claim keyed on the unique
  `(CorrelationId, UserId, Channel)` index, `Category = "LeagueInvite"` —
  idempotent across redelivery.
- `UserNotificationPreferences.LeagueInviteEnabled` gate (already exists in the
  schema; default `true`).
- `UserDevices` (NotificationsEnabled) → `SendAsync(token, title, body, data)`
  with the `data` dict above. Title "You're invited", body
  "You've been invited to {LeagueName}."
- Registered in `Program.cs`.

No new migration: `LeagueInviteEnabled` shipped in the Initial migration.

## Mobile

### Tap handler (`app/_layout.tsx`)

Implement the `handleTap` dispatch (design-only until now). Keep the existing
non-content breadcrumb log; add navigation:

- `kind === "LeagueInvite"` → route to the invite-preview screen with
  `leagueId`.
- Cold-start: if the router/auth tree isn't mounted yet, stash the intended
  route in a ref and flush once auth resolves (the
  `useLastNotificationResponse()` race called out in `notification-deep-linking.md`).

### Invite-preview screen

A small screen showing league name, description, member count, and
public/private, fetched via `GET /ui/leagues/{id}` (`LeagueDetailDto`), with
one **Join** action. (Sport and inviter aren't on that DTO today; if the
preview wants them later, widen the DTO or carry them on the event.)

- **Join** → `joinLeague(leagueId)` → forward into that league's picks page
  (`/(tabs)/picks?leagueId=...`), now a member so the page populates normally.
- **Decline / close** → dismiss; no state change.

Keeping membership acquisition on this screen leaves `picks.tsx` free of
half-membership states.

## Tests

- API: publishes `UserInvitedToPickemGroup` on a registered-email, non-member
  match; no publish for an unregistered email; no publish when the email is
  already a member.
- Notification: claim/dedupe on redelivery; suppress on `LeagueInviteEnabled =
  false`; suppress on no device; sends with the `data` payload populated.

## Out of scope (later PRs)

- **Unique-username foundation** + **invite-by-username autocomplete** on the
  league overview (web + mobile). Both reuse this same event and consumer.
- Persisting `PickemGroupInvitation` rows / an in-app invite inbox. PR1 stays
  fire-and-notify; the entity exists but is still unused.
