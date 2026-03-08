# Navigation Issue — Back Arrow & Tab Bar

## Goal

Two requirements that must coexist:

1. **Persistent bottom tab bar** — the tab bar must remain visible on every screen in the app, including detail screens (ContestOverview / GameDetail and TeamCard).
2. **Correct back navigation** — tapping the back arrow on a detail screen must return to the screen that launched it (e.g. Picks → GameDetail → back → Picks, not back → a previous GameDetail).

---

## Tech Stack

- **Expo SDK 55** / React Native 0.83.2
- **Expo Router v3** (file-based routing)
- **TypeScript 5.9.2**

---

## Current File Structure

```
app/
  _layout.tsx                        ← Root Stack (headerShown: false for tabs + auth)
  (auth)/
    _layout.tsx
    sign-in.tsx
  (tabs)/
    _layout.tsx                      ← Tabs navigator (4 visible tabs + (details) registered as href:null)
    index.tsx
    picks.tsx
    standings.tsx
    profile.tsx
    (details)/
      _layout.tsx                    ← Nested Stack (for back navigation)
      game/
        [id].tsx                     ← ContestOverview / GameDetail screen
      team/
        [slug].tsx                   ← TeamCard screen
  +not-found.tsx
  +html.tsx
```

### `app/_layout.tsx` (root)
```tsx
<Stack>
  <Stack.Screen name="(auth)" options={{ headerShown: false }} />
  <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
  <Stack.Screen name="+not-found" />
</Stack>
```

### `app/(tabs)/_layout.tsx`
```tsx
<Tabs screenOptions={{ /* tab bar styles */ }}>
  <Tabs.Screen name="index"     options={{ title: 'Home', ... }} />
  <Tabs.Screen name="picks"     options={{ title: 'Games', ... }} />
  <Tabs.Screen name="standings" options={{ title: 'Standings', ... }} />
  <Tabs.Screen name="profile"   options={{ title: 'Profile', ... }} />
  {/* Hidden detail group */}
  <Tabs.Screen name="(details)" options={{ href: null, headerShown: false }} />
</Tabs>
```

### `app/(tabs)/(details)/_layout.tsx`
```tsx
<Stack
  screenOptions={{
    headerStyle: { backgroundColor: theme.card },
    headerTintColor: theme.tint,
    headerBackTitle: 'Back',
    // ...
  }}
/>
```

---

## Symptoms

### Symptom 1 — No back arrow on GameDetail
When navigating from `picks.tsx` to `(details)/game/[id]`, **no back arrow appears** in the header. The `Stack` layout inside `(details)` exists, but the back button is absent even though there is a previous screen to return to.

### Symptom 2 — Wrong back destination (observed in a prior iteration)
In an earlier version where `game/` and `team/` each had their own `_layout.tsx` Stack, the back arrow *did* appear but navigated to the wrong screen. Example: Picks → GameDetail → TeamCard → GameDetail2 → back went to a previous GameDetail instead of TeamCard. This was because `game/` and `team/` were separate stacks with no shared history.

### Symptom 3 — Tab bar disappears (prior iteration)
When `game/[id]` and `team/[slug]` were siblings of `(tabs)` in the **root Stack** (the originally-generated approach), navigating to them replaced the entire `(tabs)` layout, hiding the tab bar completely.

---

## What Has Been Tried

| Approach | Tab bar visible? | Back arrow works? | Correct destination? |
|---|---|---|---|
| Detail screens in root Stack (original) | ❌ disappears | ✅ yes | ✅ yes |
| Separate `game/_layout` + `team/_layout` inside `(tabs)` | ✅ yes | ✅ yes | ❌ wrong screen |
| Single shared `(details)/_layout` Stack inside `(tabs)` | ✅ yes | ❌ missing | — |

---

## Desired Behavior

- Tab bar **always visible**, on tab screens and on detail screens.
- Tapping a game score on the Picks screen pushes GameDetail onto a stack; back arrow returns to Picks.
- Tapping a team name in GameDetail pushes TeamCard; back returns to GameDetail.
- Tapping a game result in TeamCard pushes another GameDetail; back returns to TeamCard.
- The chain can be arbitrarily deep — back always unwinds one step at a time in the correct order.

---

## Navigation Calls

Detail screens are reached via `router.push(...)` from within tab screens and from other detail screens:

```tsx
// From picks.tsx / GameStatus component (inside MatchupCard):
router.push(`/game/${contestId}`)

// From game/[id].tsx (TeamScoreRow):
router.push(`/team/${team.slug}`)

// From team/[slug].tsx (ScheduleRow):
router.push(`/game/${game.contestId}`)
router.push(`/team/${game.opponentSlug}`)
```

---

## Key Question for Claude Code

What is the correct Expo Router v3 structure to achieve a **persistent tab bar on all screens** while maintaining **proper linear push/pop back navigation** across screens of mixed types (game detail, team card)?

Please propose a concrete file structure and layout implementation. Consider whether a custom tab bar component rendered outside the navigator tree (e.g. as an overlay or via a shared layout root trick) might be the cleanest long-term solution, versus a navigator nesting approach.

---

## Resolution

The issue was solved with a custom `BackButton` component in `app/(tabs)/(details)/_layout.tsx`. The approach:

1. A `BackButton` component uses `useNavigationState` from `@react-navigation/native` to locate the currently focused route within the navigation state tree, then reads `backTitle` from that route's `params`. This is necessary because `useLocalSearchParams` returns the layout's own params (which do not include `backTitle`), whereas `useNavigationState` gives access to the focused child screen's params. The component traverses `state.routes` to find the focused index, then accesses `route.params?.backTitle`.
2. The Stack's `screenOptions` sets `headerBackVisible: false` to hide the default back button and `headerLeft: () => <BackButton />` to render the custom component.
3. The `BackButton` calls `router.back()` on press and displays the `backTitle` param (falling back to "Back" if not provided).
4. Callers pass the `backTitle` param when navigating, e.g. `router.push({ pathname: '/game/[id]', params: { id: contestId, backTitle: 'Picks' } })`.

This gives full control over back-button rendering and label text while preserving the persistent tab bar (since detail screens remain nested inside the `(tabs)` group).
