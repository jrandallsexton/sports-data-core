# Mobile app — MLB friends-&-family beta readiness

**Purpose:** living punch list to get the sd-mobile app ready for a friends MLB
beta via TestFlight/EAS. Beta is **MLB-only**, closed group. From the audit on
2026-07-14. See [[project_mobile_first_for_mlb_beta]].

**Bottom line:** the core loop (sign-in → create/join league → make picks →
results → standings) works and is genuinely MLB-aware (live baseball card with
inning/count/outs/baserunners, "Closest Runs" tiebreaker, 6 divisions, probable
pitchers, correct result rendering). Remaining blockers are small — mostly copy
and one real feature gap (permission priming).

## Tier 1 — blockers (before friends get the build)

- [ ] **Notification permission priming.** No automatic permission prompt exists;
  registration is silent (`useRegisterPushDevice`, `prompt:false`) or via the
  manual settings button. Without a prompt, testers get **no notifications** by
  default. Add a one-time prime after a user joins/creates their first league,
  using `requestNotificationPermission()` + `registerThisDevice()`
  (`src/lib/notifications/`). *This is the planned next feature.*
- [ ] **Sign-in tagline ignores MLB** — `app/(auth)/sign-in.tsx:137` reads
  "NCAAFB & NFL Pick'em". First screen an MLB tester sees. Make sport-neutral.
- [ ] **"Forgot password?" is a dead link** — `app/(auth)/sign-in.tsx:271`, no
  handler. Wire Firebase `sendPasswordResetEmail` or hide/stub it. (Only affects
  email/password users; most use Google/Apple.)

## Tier 2 — rough edges (early in the beta)

- [ ] Remove **temp Sentry diagnostics** in `app/_layout.tsx` (lines ~217/275/327,
  marked "remove once confirmed") — tap routing is verified working.
- [ ] **Standings shows only the first league** — `app/(tabs)/standings.tsx:78`
  hardcodes `me?.leagues?.[0]`. Add a league selector.
- [ ] **Empty-week copy** "check back closer to the season"
  (`app/(tabs)/picks.tsx:~356`) misleads mid-season.
- [ ] **Welcome copy "every week"** (`app/(auth)/welcome.tsx:45`) — football-centric.
- [ ] `PrimarySlotOffSeasonCountdown.tsx` omits MLB from its sports array —
  one-line add. Low impact while MLB is in-season (testers hit live content /
  the "create a league" card, not the countdown).
- [ ] Team-detail **`CURRENT_YEAR = 2025` hardcode**
  (`app/(tabs)/(details)/sport/[sport]/[league]/team/[slug].tsx:164`) — needs a
  call on whether 2026 season data is sourced before flipping to `getFullYear()`.

## Tier 3 — defer (not beta-gating)

- Custom app icon / splash branding (placeholder today).
- Version/build-number tidy-up — `version` 0.1.0; **iOS build 34 vs Android
  `versionCode` 2** (Android barely built — ties to the Android deployment issue).
- Week-based navigation terminology for a date-based sport.
- Public-league discovery on mobile (web-only today).
- Profile win/loss stats stub (0-0 placeholder).
- Web Google sign-in (throws; beta is mobile).
- Deep-link routing for non-`LeagueInvite` notification kinds (they land safely).

## Corrections to note (audit over-flagged these)

- **iOS notification usage-description is NOT required.** Modern iOS notification
  prompts use OS-supplied text; no `app.json` Info.plist string needed. Not a
  blocker.

## Sequence

Audit (done) → **Android deployment issue** → **permission priming** → Tier 1
copy/link fixes → Tier 2. Backend (StatBot previews, MetricBot predictions) runs
in parallel while friends test.
