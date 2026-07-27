# sd-mobile Pre-Submission Review — 2026-07-27

Scope: full review of `src/UI/sd-mobile` (~15k LOC, 90 source files) ahead of
Play Store submission and closed testing. Lenses: structural, best practices,
security, performance. Code-level findings were verified against the source,
not inferred. Store-policy and operational statements (Google Play / App Store
requirements, e.g. the closed-testing tester rule in §5) reflect published
policies as understood at the time of writing and are NOT code-verifiable —
confirm current requirements in the Play Console / App Store Connect when
acting on them.

## Verdict

The codebase is in genuinely good shape — notably strong for a solo project.
Standouts: the auth/session bootstrap (`firebase.ts` platform-split with
explicit failure semantics), sign-out sequencing (device unregister →
`signOut` → `queryClient.clear()` to prevent cross-account cache leakage),
the push cold-start race handling in `_layout.tsx`, single-flight installation
ID minting, and OTA apply-on-return policy. TypeScript `strict` is on and only
5 `any`s exist in app code. Nothing here is submission-blocking at the code
level. The items below are ordered by how much they matter.

## 1. Before the store build

### 1.1 TEMP push diagnostics still ship (app/_layout.tsx)
RESOLVED 2026-07-27: all three `(diag)` Sentry captures removed after the
deep-link was confirmed end-to-end on iOS and Android. The privacy-safe
`[push] tapped` / `[push] received` console breadcrumbs were kept — they're
deliberate crash context (documented in-file), not TEMP.

### 1.2 Version identity is split
RESOLVED 2026-07-27: launch version decided as **1.0.0** — app.json and
package.json both bumped ahead of the closed-testing build. The inert
`android.versionCode` / `ios.buildNumber` were deleted from app.json (EAS
remote versioning owns them). Note for the future: `runtimeVersion.policy:
"appVersion"` means installed 0.1.0 preview builds no longer receive OTA
updates published for 1.0.0 — expected and correct; testers move to the new
build.

### 1.3 Sentry user email → Play Data Safety form
RESOLVED 2026-07-27: `Sentry.setUser` now sends uid only — email dropped.
Crash correlation still works via uid; the Data Safety declaration for Sentry
simplifies to identifiers + diagnostics. NOTE: the Privacy Policy's crash-report
paragraph still says reports "may include your user identifier and email" —
now over-declares; safe direction, but can be tightened at the next policy
edit.

Data the app demonstrably sends off-device (for the form): Firebase auth
identity (email/display name), FCM token + installation UUID (own API),
crash/error telemetry (Sentry), notification preferences, picks/league
activity (own API).

### 1.4 Play account-deletion web requirement
In-app deletion exists (`profile.tsx` → `usersApi.deleteAccount`) — the hard
part is done. Play additionally requires a **web URL** for account deletion in
the Data Safety section (users who uninstalled must be able to request
deletion without reinstalling). Ensure sportdeets.com has (or gets) that page
before filling in the form.

## 2. Security

### 2.1 Auth session in plaintext AsyncStorage (accepted risk — document it)
`initializeAuth(app, { persistence: getReactNativePersistence(AsyncStorage) })`
stores the Firebase session — including the refresh token — unencrypted in
AsyncStorage. This is the documented Firebase-JS-SDK-on-RN pattern and the
practical risk requires a compromised/rooted device, but `expo-secure-store`
is the stricter home for token material. A custom `Persistence` adapter over
SecureStore is possible (mind its ~2KB value limit — the common pattern is an
AsyncStorage payload encrypted with a SecureStore-held key). Reasonable to
accept as-is for launch; worth a backlog entry rather than silence.

### 2.2 league-invite join semantics (server-side question, not a mobile bug)
The deep-link screen validates the GUID shape client-side and treats
possession of a `leagueId` as sufficient to render Join. That makes the
leagueId a capability token: anyone authenticated who obtains a private
league's GUID can join it (mobile never sees an invite nonce). If
`POST joinLeague` doesn't distinguish invited-vs-uninvited users server-side,
that's worth a deliberate decision before growth. GUIDs are unguessable in
practice; they do leak via screenshots/shares.

### 2.3 Clean findings (no action)
- No secrets in the repo: `.env.local` untracked; `eas.json` env values are
  all `EXPO_PUBLIC_*` (client-shipped by definition); `google-services.json` /
  `GoogleService-Info.plist` tracked, which Google explicitly permits.
- HTTPS everywhere in release (EAS env pins `https://api.sportdeets.com`;
  Android blocks cleartext by default).
- `admin/push-token` screen is defense-in-depth gated on `me.isAdmin` despite
  also being link-gated — right convention.
- Sign-out clears Google session, unregisters the device, and purges the
  query cache. No cross-account leak path found.
- Console logging is disciplined (26 call sites, none logging tokens or PII;
  the push handlers explicitly log only non-content fields).

## 3. Performance

### 3.1 SignalR receives all live traffic for all users (BE-shaped, note only)
The hub subscription is app-wide: every authed client receives every
`FootballPlayCompleted` / `BaseballPlayCompleted` for every live contest,
whether or not the user views any of them. The mobile side is defensively
built — foreground-only connection (AppState stop/start), per-contest
selector (`useContestUpdate`) so only affected `MatchupCard`s re-render — so
the client cost is store writes, radio wake-ups, and payload parse. Fine at
beta scale; at football-Saturday scale (dozens of concurrent games ×
play-by-play), per-contest/per-league SignalR groups server-side is the lever.
Backend work; parked.

### 3.2 Poll + push overlap on the game screen
`useContestOverview` polls every 30s while mounted, and SignalR pushes the
same game's plays. Redundant but bounded, and the poll covers reconnect gaps —
acceptable. If battery complaints surface, gate the poll on
`status !== live || signalR disconnected`.

### 3.3 Clean findings (no action)
- Long lists (`picks`, `standings`) use `FlatList`; the `ScrollView`+`.map`
  screens (welcome, create-league, settings, leagues grid, game box score)
  all render bounded content.
- No polling loops beyond the two above (the countdown ticks hourly).
- React Query defaults are sane (5m stale / 30m gc, no focus refetch on
  mobile, 401s never retried).
- Zustand store handlers replace only the affected contest record; selector
  equality keeps the blast radius to that contest's card.
- Optional: team/league logos render via RN `Image`; `expo-image` would add
  proper disk caching for the repeated-logo case. Cosmetic-level win.

## 4. Structural / best practices

### 4.1 Dual Firebase stacks — sound, keep documented
Firebase JS SDK for auth + `@react-native-firebase` for messaging is a
deliberate hybrid (JS SDK has no FCM path on iOS; RNFB handles APNs→FCM).
The rationale comment in `pushNotifications.ts` is exactly the documentation
future-you needs. No change; just don't let a third Firebase surface creep in.

### 4.2 `UserPick`'s index signature undermines strict mode
RESOLVED 2026-07-27: removed. Zero fallout — `tsc --noEmit` clean on first
run, confirming the hole was load-bearing for nothing.

### 4.3 21 `as never` router casts
All are the documented typed-routes generator gap. After the next
`expo start` regenerates `.expo/types`, sweep how many still need the cast —
each one is a spot where a route rename breaks silently.

### 4.4 Test suite shape
84 tests, all hooks/lib/api — zero component/screen tests. The money paths
with real regression history (picks header state machine, DateField platform
branches, AuthGuard redirects, deep-link flush guards) are exactly the
untested ones. `@testing-library/react-native` is already a devDependency.
Even 3–4 screen tests on picks + sign-in would catch the class of bug this
review keeps finding fixed-by-hand.

### 4.5 Local cruft
RESOLVED 2026-07-27: logs deleted; `dist/`, `coverage/`, `junit.xml` added to
the repo-root `.gitignore` under an sd-mobile section. `reinstall.ps1` kept —
it's a real utility (nvm-pinned clean reinstall), not cruft.

## 5. Android-specific submission notes

- **POST_NOTIFICATIONS (Android 13+)**: RESOLVED 2026-07-27. Confirmed on the
  founder's own device — the silent-only registration meant an Android install
  received zero pushes until the settings screen's manual action was found.
  `useRegisterPushDevice` now prompts ONCE on Android at the first
  authenticated attempt (AsyncStorage `push-permission-prompted` marker); a
  denial is respected and the settings action remains the re-ask path. iOS
  stays fully silent by design.
- `predictiveBackGestureEnabled: false` — deliberate, fine for now; Google is
  pushing this toward default-on, revisit post-launch.
- Closed testing: Google requires 12+ testers for 14 days for new personal
  accounts; if the account is a business/org account this may not apply —
  check which rules bind before planning the timeline.

## Suggested order of attack

1. Remove TEMP diagnostics once deep-link confirmed on device (1.1)
2. Settle version identity (1.2) — one-line decisions, do before first build
3. Drop email from Sentry.setUser (1.3)
4. Account-deletion web URL + Data Safety form (1.4)
5. Decide Android notification prompt strategy (§5)
6. `UserPick` index signature + gitignore/cruft (4.2, 4.5)
7. Backlog: SecureStore persistence, SignalR groups, screen tests, expo-image
