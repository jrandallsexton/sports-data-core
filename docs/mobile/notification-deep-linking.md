# Notification Deep-Linking

Status: design (not yet implemented)
Last updated: 2026-06-29

## Goal

When a user taps a push notification, take them to the right place instead of
just opening the app cold. The first two use cases, both for the kickoff /
"unpicked games starting soon" reminder:

1. **Multiple unpicked games starting at the same time** → open the **picks
   page** with the relevant league pre-selected.
2. **A single unpicked game** → open the picks page **and auto-scroll the list
   directly to that game's matchup card**.

## Key realization: both cases land on the same screen

The "matchup card" in use case 2 is the **list item** rendered on the picks tab
(`app/(tabs)/picks.tsx`), a `FlatList` keyed by `item.matchup.contestId` — not
the separate game-detail page (`app/(tabs)/(details)/sport/[sport]/[league]/game/[id].tsx`).

So both use cases route to `/(tabs)/picks`. The only difference is whether we
scroll to a specific card after arrival. Consequences:

- We do **not** need `sport` / `league` route strings in the notification
  payload — the picks page resolves those itself from the matchup response
  (`sportLinks.ts`). The payload only needs IDs.
- The picks page **already** reads a `leagueId` deep-link param and
  auto-selects that league + week on arrival (`picks.tsx`, ~lines 38–62). Use
  case 1 therefore already works with no new screen behavior.

## Wire contract (FCM `data` payload)

The Notification service attaches a `data` dictionary to the FCM message. FCM
data values must be strings.

```jsonc
// multiple unpicked games at this kickoff slot
{ "kind": "KickoffReminder", "target": "picks",   "leagueId": "<guid>", "week": "3" }

// single unpicked game
{ "kind": "KickoffReminder", "target": "matchup", "leagueId": "<guid>", "week": "3", "contestId": "<guid>" }
```

Everything here already exists in the Notification projections — no new
projected columns and no slug needed:

- `PickemGroupMatchups` — `ContestId`, `PickemGroupId`, `StartDateUtc`,
  `SeasonWeek`
- `UserPicks` — `(UserId, ContestId, PickemGroupId)`
- `PickemGroupMembers` — `(UserId, PickemGroupId)`

## Backend: where the single-vs-multiple decision is made

The decision is a dispatch-time computation. The current
`NotificationDispatcher.SendContestStartReminderAsync` is **per-contest** (one
fire per game), which can't express "you have N unpicked games starting at
1:00". These use cases need the reminder **consolidated per (user, kickoff
slot)**. At fire time the dispatcher:

1. Computes the user's **unpicked** games whose `StartDateUtc` falls in this
   kickoff slot: matchups in the user's leagues (`PickemGroupMembers` →
   `PickemGroupMatchups`) with no matching `UserPicks` row.
2. Chooses the payload:
   - `count == 0` → suppress (nothing to remind).
   - `count == 1` → `target: "matchup"` + that `contestId`.
   - `count >= 2` → `target: "picks"`.
3. Builds the `data` dict and passes it as the 4th arg to
   `IPushNotificationSender.SendAsync(token, title, body, data)`.

`SendAsync` already accepts `IReadOnlyDictionary<string,string> data` and
`FirebasePushNotificationSender` already maps it onto `Message.Data` — but
**no current caller populates it** (all five dispatch sites call the
3-arg form). So the sender plumbing is additive only.

The claim / dedupe / device-gate / prefs plumbing is unchanged — it's the same
atomic-claim pattern the dispatcher already uses.

> **Open design choice (the only non-wiring piece):** consolidating by kickoff
> slot changes the reminder's grain from per-contest to per-(user, start time).
> This affects scheduling (one Hangfire job per user per slot rather than per
> contest) and the `PendingScheduledJob` natural key. Resolve before
> implementation.

## Mobile: tap handler (`app/_layout.tsx`)

`NativePushDiagnostics` already captures taps for both cold-start
(`Notifications.useLastNotificationResponse()`) and foreground
(`addNotificationResponseReceivedListener`), with per-id dedupe. Today it logs a
deliberately **non-content** breadcrumb — only `data.kind` and
`actionIdentifier`, never payload content. **Keep that log** (it's the
privacy-safe tap diagnostic) and add the `router.push` navigation *alongside*
it, rather than replacing it:

```tsx
const handleTap = (response: Notifications.NotificationResponse) => {
  const data = response.notification.request.content.data ?? {};

  // Existing non-content breadcrumb — keep it.
  console.log('[push] tapped', { id: response.notification.request.identifier, kind: data.kind });

  // Added: navigate for KickoffReminder taps.
  if (data.kind === 'KickoffReminder' && data.leagueId) {
    router.push({
      pathname: '/(tabs)/picks',
      params: {
        leagueId: data.leagueId,
        week: data.week,
        ...(data.target === 'matchup' ? { contestId: data.contestId } : {}),
      },
    });
  }
};
```

**Cold-start gotcha:** if the app was killed, the router/tab tree and the auth
guard may not be mounted when `useLastNotificationResponse()` first returns.
Stash the intended route in a ref and flush it once auth resolves; otherwise the
`router.push` races the redirect-to-sign-in. Foreground/background taps are
unaffected.

## Mobile: auto-scroll to the matchup card (`app/(tabs)/picks.tsx`)

The list is a plain `FlatList` (not FlashList), keyed by `contestId`, with **no
ref or `scrollToIndex` today**. Three hazards, each handled.

### 1. Timing — scroll only after the data settles

The target row isn't in `visibleEntries` until the league + week are selected
and matchups have fetched. Scroll in an effect that re-runs as data changes,
not on mount:

```tsx
const listRef = useRef<FlatList>(null);
const { contestId: scrollTo } = useLocalSearchParams<{ contestId?: string }>();
const didScroll = useRef(false);

useEffect(() => { didScroll.current = false; }, [scrollTo]); // re-arm on new deep link

useEffect(() => {
  if (!scrollTo || didScroll.current || visibleEntries.length === 0) return;
  const index = visibleEntries.findIndex(e => e.matchup.contestId === scrollTo);
  if (index < 0) return;             // wrong week loaded yet → bail; effect re-runs on data change
  didScroll.current = true;           // one-shot
  requestAnimationFrame(() =>
    listRef.current?.scrollToIndex({ index, animated: true, viewPosition: 0.3 }));
}, [scrollTo, visibleEntries]);
```

### 2. Virtualization failure — guard `scrollToIndex`

`scrollToIndex` on a not-yet-rendered (virtualized) row throws unless guarded.
Add the official escape hatch to the `FlatList`:

```tsx
onScrollToIndexFailed={(info) => {
  listRef.current?.scrollToOffset({ offset: info.averageItemLength * info.index, animated: false });
  setTimeout(() => listRef.current?.scrollToIndex({ index: info.index, animated: true, viewPosition: 0.3 }), 50);
}}
```

If `MatchupCard` is a **fixed** height, prefer a `getItemLayout` instead —
`scrollToIndex` then becomes instant/reliable and `onScrollToIndexFailed` can be
dropped. Card height currently varies (picked / scheduled / tiebreaker states),
so `onScrollToIndexFailed` is the safe default unless the card is normalized to
a fixed height.

### 3. Select the correct week first

The card only exists in `visibleEntries` if the matching week is active. Pass
`week` in the deep link and have the existing league-select effect honor it (it
currently defaults to `latestWeek`). For a kickoff reminder this is usually the
current week, but passing it explicitly prevents case 2 silently degrading to
"landed on the list, no scroll."

### Polish (optional)

- `viewPosition: 0.3` parks the card ~30% from the top so it reads as the focus
  rather than jammed at the screen edge.
- A brief highlight pulse on the target card on arrival makes "why am I here"
  obvious. Pass `contestId` to the card and flash its border once.

## Scope / non-goals

- Only the kickoff / unpicked-games reminder is covered here. Other notification
  types (pick results, line moves, league invites) will deep-link later and can
  reuse the same `data.kind` + tap-handler dispatch pattern.
- No new routes, no new dependencies. The `sportdeets` URL scheme is already set
  (`app.json`).
- The richer game-detail page (`/sport/[sport]/[league]/game/[id]`) is **not**
  the target for these two cases; that route is a candidate for future
  "your pick was scored" deep links, where it would need `sport` + `league`
  route strings derived from the `Sport` enum.

## File touchpoints

| Area | File |
|------|------|
| Dispatch decision + populate `data` | `src/SportsData.Notification/Application/Dispatching/NotificationDispatcher.cs` |
| Sender (already supports `data`) | `src/SportsData.Notification/Infrastructure/Notifications/{IPushNotificationSender,FirebasePushNotificationSender}.cs` |
| Tap → navigate | `src/UI/sd-mobile/app/_layout.tsx` (`NativePushDiagnostics`) |
| Auto-scroll + week honoring | `src/UI/sd-mobile/app/(tabs)/picks.tsx` |
| Matchup card (height / highlight) | `src/UI/sd-mobile/src/components/features/games/MatchupCard.tsx` |
| Related planning | `docs/mobile/notifications-and-live-updates.md`, `docs/mobile/device-identity-and-push-ownership.md` |
