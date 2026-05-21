# Text size user setting

Companion to `expo-deployment-model.md`. Captures the design for a
user-facing text-size preference (S | M | L) layered on top of the
existing theme setting.

Status: **implemented** as of 2026-05-21 (PR #346). Forward-looking
sections below ("Current state", "Recommended sequence") preserve the
original planning content for posterity — what actually shipped is
summarized here:

- `src/lib/textSize/TextSizeContext.tsx` — provider, `useTextSize`,
  `useTextScale`, AsyncStorage persistence, `isHydrated` flag.
- `src/components/ui/AppText.tsx` — drop-in `<Text>` wrapper that
  multiplies `fontSize` by the current scale; `allowFontScaling={false}`.
- `app/_layout.tsx` — `<TextSizeProvider>` wraps `<RootLayoutNav>`;
  splash `hideAsync()` waits on theme AND text-size hydration.
- `app/(tabs)/profile.tsx` — second `SegmentedControl` (S | M | L)
  in the Appearance section with the hint "Affects all in-app text.
  Header brand stays fixed."
- 22 component / screen files migrated from
  `import { Text } from 'react-native'` to
  `import { Text } from '@/src/components/ui/AppText'`. `Wordmark.tsx`
  is intentionally excluded.

---

## Why now

Two pressures pushed this up the queue:

- iPhone 14 / iPhone 12 Pro readability work in mobile MatchupCard
  Round 3 (PR #341) shipped font bumps in `MatchupCard` and
  `GameStatus` to fix small-text complaints. That fix was global —
  everyone gets the new larger sizes. Some users will want smaller
  (more density), some will want larger (accessibility). One-size-fits-all
  is the wrong default.
- Theme already has a user preference (`light` / `dark` / `system`)
  and a clean implementation pattern via `ThemeContext`. Text size is
  the natural sibling — same persistence, same hydration gate, same
  settings UI slot. Adding it doesn't introduce a new architectural
  concept.

---

## Scope

### In scope

- Three options surfaced as a `SegmentedControl`: **S | M | L**.
- Stored per-user in AsyncStorage. Mirrors the theme persistence
  pattern; doesn't sync to the server (yet).
- Applies to **all app text** rendered through the canonical Text
  component. Modals, screens, headers, the works.

### Out of scope (won't fix in this PR)

- Per-surface granular control (one global setting, not "Picks: L,
  Standings: M").
- Line height, letter spacing, font weight — only `fontSize` scales.
- Tab bar labels — they're laid out with fixed dimensions in the
  Tabs navigator; scaling them risks overflow into the tab bar's
  fixed 90/72pt height.
- Wordmark text — the brand lockup uses its own size prop
  (`<Wordmark size={20} />`). Scaling brand glyphs alongside body
  text would shift visual hierarchy in ways the design doesn't want.
- TextInput sizing — RN `TextInput` has its own font-size path.
  Sign-in form fields will not scale in this PR.
- Server-side sync (so the preference roams across devices). Worth
  doing later but the API endpoint and `UserDto` field would need
  matching changes; defer.

---

## Three concepts

### 1. Scale values

| Option | Multiplier | Effect |
| ------ | ---------- | ------ |
| `'small'` (S) | **1.00** | Current sizes (post-Round-3 readability bumps) — no change |
| `'medium'` (M) | **1.125** | +12.5%. ~16pt body becomes ~18pt |
| `'large'` (L) | **1.25** | +25%. ~16pt body becomes ~20pt |

Defaults to **small** so existing users see no visual change after
the feature ships. Larger sizes are opt-in.

Rationale for `1.125` / `1.25`: matches web accessibility convention
("100/112/125" steps). Tight enough that layouts don't break;
visible enough that the user feels a real difference between steps.

### 2. The `<AppText>` wrapper

React Native renders text via the `<Text>` primitive. There's no
parent-to-child font-size cascade — every Text component sets its
own size. With **150 explicit `fontSize: <number>` declarations
across 23 files** in the mobile app today, retroactively scaling
them via a global mechanism requires either:

- A wrapper component that multiplies `fontSize` at render time, OR
- A migration to semantic tokens (`Typography.body`, etc.).

This doc picks the wrapper. New file:

```tsx
// src/components/ui/AppText.tsx
import { Text as RNText, StyleSheet, type TextProps } from 'react-native';
import { useTextScale } from '@/src/lib/textSize/TextSizeContext';

export function Text(props: TextProps) {
  const scale = useTextScale();
  const flat = StyleSheet.flatten(props.style);
  const scaled =
    flat && typeof flat.fontSize === 'number'
      ? { ...flat, fontSize: flat.fontSize * scale }
      : flat;
  return (
    <RNText
      // Disable OS-level Dynamic Type scaling — our user setting is the
      // single source of truth. Without this, large iOS Settings → Display
      // text would compound with our multiplier and produce surprise jumps.
      allowFontScaling={false}
      {...props}
      style={scaled}
    />
  );
}
```

Then the 23 files swap:

```tsx
import { Text } from 'react-native';
// →
import { Text } from '@/src/components/ui/AppText';
```

Identical API, drop-in. Any future component that imports `Text`
from the new path gets scaling automatically.

### 3. Persistence + hydration

Mirror `ThemeContext` line-for-line:

- AsyncStorage key: `text-size`
- Persisted values: `'small' | 'medium' | 'large'`
- `isHydrated` flag — splash stays up until both theme AND text-size
  preferences are read. Without this, a user with stored `'large'`
  would see a brief paint at `'small'` before snapping bigger.

---

## Current state

Nothing relevant exists yet:

- No `TextSizeContext` or related hook.
- No `AppText` wrapper — all 23 files import `Text` directly from
  `react-native`.
- No settings UI control. The "Appearance" section in
  `app/(tabs)/profile.tsx` only has the theme `SegmentedControl`.
- Splash gating in `app/_layout.tsx` waits on theme hydration only.

---

## Recommended sequence

| # | Step | Outcome |
| - | ---- | ------- |
| 1 | Add `src/lib/textSize/TextSizeContext.tsx` mirroring `ThemeContext` | Persistence + hook + `isHydrated` flag |
| 2 | Wrap `<TextSizeProvider>` around the existing `<ThemeProvider>` in `app/_layout.tsx` | Context available app-wide |
| 3 | Update splash hide gate to wait on both `isHydrated` flags | No font-size-flash on launch |
| 4 | Add `src/components/ui/AppText.tsx` | The canonical `<Text>` for the app |
| 5 | Migrate the 23 files to import `Text` from `AppText` | Scaling applies everywhere |
| 6 | Add "Text Size" `SegmentedControl` to the Appearance section in `profile.tsx` | User can change the setting |
| 7 | Visual smoke test on iPhone 14 — toggle S/M/L, confirm MatchupCard / picks / sign-in all scale | Confirms the wrapper is in the right places |

Steps 1–6 are mechanical and low-risk. Step 5 is the bulk of the
diff but is a global find/replace per file.

---

## Decisions to make before editing config

### 1. Storage key + value convention

- **(a)** Key `text-size`, values `'small' | 'medium' | 'large'`
- **(b)** Key `font-scale`, values `1.0 | 1.125 | 1.25` (raw multipliers)

Recommendation: **(a)**. Symbolic names match the theme pattern
(`light` / `dark` / `system`) and stay readable when you grep
AsyncStorage. The multiplier is an implementation detail derived
from the symbolic name inside the context.

### 2. Where does the settings UI go?

- **(a)** New "Text Size" section in `profile.tsx`, below "Appearance"
- **(b)** Same "Appearance" section, second `SegmentedControl`
  stacked below the theme one

Recommendation: **(b)**. Both controls are visual-preference
adjusters; grouping them keeps the screen tidy. The "System follows
your device's light/dark setting" hint already exists in
Appearance — adding a parallel hint under the text-size control
("Affects all in-app text. Header brand stays fixed.") fits the
same pattern.

### 3. `allowFontScaling` behavior

- **(a)** Wrapper sets `allowFontScaling={false}` — our setting is
  the sole source of truth
- **(b)** Pass through to props default (`true`) — iOS Dynamic Type
  compounds with our setting

Recommendation: **(a)**. Compounding is a usability trap; a user on
iOS "XXL" Dynamic Type who then picks our "L" gets an enormous
result they didn't expect. Single source of truth is the right
default; the OS-level setting still affects everything outside our
app.

### 4. Should the `Wordmark` component scale?

- **(a)** No — brand stays fixed
- **(b)** Yes — wordmark text grows with the rest of the UI

Recommendation: **(a)**. The Wordmark is brand, not body content.
Scaling it would shift visual hierarchy on every screen where it
appears (auth header, tab headers, sign-in). It's intentional that
brand glyphs hold their relative weight.

---

## What changes when we execute

### New files

```
src/UI/sd-mobile/src/lib/textSize/TextSizeContext.tsx  (~90 lines)
src/UI/sd-mobile/src/components/ui/AppText.tsx          (~25 lines)
```

### Modified

- `app/_layout.tsx` — wrap `<TextSizeProvider>`, expand splash gate
  to wait on both hydration flags.
- `app/(tabs)/profile.tsx` — add the new SegmentedControl in the
  Appearance section.
- The 23 files listed in the migration section below — change `Text`
  import path.

### Migration mechanics — 23 files, ~150 declarations

```text
app/create-league.tsx                                          (6 declarations)
app/+not-found.tsx                                              (2)
app/(auth)/sign-in.tsx                                          (7)
app/(tabs)/_layout.tsx                                          (2)
app/(tabs)/picks.tsx                                            (2)
app/(tabs)/profile.tsx                                         (10)
app/(tabs)/standings.tsx                                        (6)
app/(tabs)/(details)/_layout.tsx                                (1)
app/(tabs)/(details)/sport/[sport]/[league]/game/[id].tsx     (18)
app/(tabs)/(details)/sport/[sport]/[league]/team/[slug].tsx   (11)
src/components/ui/SegmentedControl.tsx                          (1)
src/components/ui/Button.tsx                                    (3)
src/components/ui/EmptyState.tsx                                (3)
src/components/ui/LoadingSpinner.tsx                            (1)
src/components/features/games/GameStatus.tsx                   (13)
src/components/features/games/InsightModal.tsx                 (14)
src/components/features/games/MatchupCard.tsx                  (18)
src/components/features/games/StatsComparisonModal.tsx          (9)
src/components/features/home/PrimarySlotNewUser.tsx             (3)
src/components/features/home/PrimarySlotOffSeasonCountdown.tsx  (3)
src/components/features/home/YourLeaguesCard.tsx                (3)
src/components/features/selectors/LeagueWeekSelector.tsx        (3)
src/components/features/settings/TimezonePickerModal.tsx       (11)
```

The migration is mechanical: per file, change the `Text` import
from `react-native` to `@/src/components/ui/AppText`. The
`fontSize:` values stay as-is — they're now scale baselines, not
final sizes.

Files NOT in the migration:

- `src/components/brand/Wordmark.tsx` — keeps `Text` from
  `react-native` directly, scales via its own `size` prop. See
  decision 4 above.

---

## Steady-state usage

Once shipped:

- New components should `import { Text } from '@/src/components/ui/AppText'`
  by default. Lint rule maybe (deferred — convention first, enforcement
  if drift becomes a problem).
- New `fontSize` values declared anywhere in the app are baselines.
  They get multiplied at render time. The number written in code is
  what "small" users see.
- Brand-locked surfaces (Wordmark, eventually badges if we add them)
  import `Text` from `react-native` directly to opt out.

---

## Gotchas to watch for

1. **Layout overflow at L.** A 25% increase can push fixed-width
   text into wrap or truncation territory. The Round 3 stack
   layout is robust (it's vertical) but anything with `numberOfLines={1}`
   plus a fixed container width is at risk. Visual smoke test should
   exercise: MatchupCard team rows at L (Cleveland Guardians +
   3-digit record), Picks selector at L, Standings tables at L.
2. **Tab bar height.** Tab labels are NOT scaled (out of scope),
   but if anyone forgets and migrates `_layout.tsx`'s tab styles to
   AppText, the bottom tabs will distort. The intentional carve-out
   needs a code comment.
3. **Modals.** RN modals can clip at the edges if internal text
   wraps unexpectedly. Test `InsightModal`, `StatsComparisonModal`,
   `TimezonePickerModal` at L.
4. **TextInput.** Form inputs (sign-in) won't scale until a follow-up
   PR. Worth noting in the user-facing hint text under the control
   ("Affects all in-app text" is slightly aspirational right now).
5. **iOS Dynamic Type.** Setting `allowFontScaling={false}` is a
   deliberate trade-off — power users who rely on OS-level Dynamic
   Type will get less than they expect from our app. Mitigation: our
   in-app S/M/L gives the same effect; the choice is whether to
   surface that in the "Affects all in-app text" hint.
6. **Static rendering / web export.** The `eas update --platform=all`
   web bundle pass evaluates code paths during static rendering. If
   `TextSizeContext` is consumed at module-evaluation time anywhere
   (it shouldn't be — only in render), the web export could choke.
   Mirror the `Platform.OS === 'web'` defensive pattern from
   `firebase.ts` (PR #345) if the smoke test trips.

---

## References

- `src/lib/theme/ThemeContext.tsx` — the canonical pattern this
  feature mirrors line-for-line.
- `app/(tabs)/profile.tsx` — settings UI placement; the existing
  theme `SegmentedControl` is the visual model.
- `docs/ui/matchup-card-blueprint.md` — Round 3 readability bumps
  context that motivated this feature.
- `docs/mobile/eas-update.md` — sibling user-preference / settings
  doc style for tone and structure.
- Web parity: sd-ui has no font-size preference yet either; if this
  ships well on mobile we should mirror on web.
