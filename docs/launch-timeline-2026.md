# 2026 Season Launch Timeline

Status: living plan — dates are targets, adjust as reality moves.
Last updated: 2026-07-11

## Anchors

| Event | Date | Days out (from 2026-07-11) |
|-------|------|-----------|
| **Today** | 2026-07-11 | — |
| **Start Android closed test** (12 testers, 14-day clock) | **this week** | 0–5 |
| Android closed test complete (14 days) | ~2026-07-28 | ~17 |
| Mobile blockers complete | 2026-08-01 | 21 |
| Code freeze + final builds | 2026-08-03 | 23 |
| **Submit iOS + apply for Android production** | **~2026-08-04** | **24** |
| Target: **live on both stores** | **~2026-08-14** | **34** |
| **NCAAFB kickoff** | **2026-09-05** | **56** |
| **NFL kickoff** | **2026-09-10** | **61** |

Guiding constraint (founder's ask): live on the stores ~2+ weeks before NCAAFB,
**on both platforms**. Submission pulled **earlier** for buffer — sound, because the
first approval is the slow/risky one and everything after is cheap (Apple updates
< 48 h; most fixes ship via **EAS OTA with no review at all** — only native changes
need a new store build).

### Both platforms are launch-critical — Android is primary
Most of the founder's launch cohort (the pick'em-league friends) is on **Android**,
so Android is **not** a fast-follow — both stores must be live by kickoff.

- **Google Play gate:** personal (post-Nov-2023) account → **closed test with 12
  testers, opted-in ≥14 continuous days**, before applying for production. This is
  the **critical path** → start this week. The league friends *are* the 12 testers
  (= the launch cohort). No LLC / org-account path.
- **Decoupled from the blocker sprint:** the 14 days is a *track-duration* clock, not
  a build gate — upload a **functional** build now to start the clock, keep polishing
  in parallel, push the final build for production after the 14 days.
- **iOS** has no tester gate; submit in the same window (~Aug 4), live ~Aug 14.

### Review-latency assumptions
- **Apple**: typically < 24–48 h, no new-developer penalty; budget for **one
  rejection cycle** (Apple Sign-In / account-deletion are the 4.8 / 5.1.1(v)
  triggers — both built). Expedited review available for emergencies.
- **Google Play**: the **14-day closed test is the gate**, not review speed. Once
  production access is granted and the first app is approved, later updates are
  fast (hours–1 day).
- **EAS OTA**: post-launch, JS-only fixes ship via `eas update` with **zero store
  review**. Only native changes (deps, entitlements, permissions, version bumps)
  need a fresh store build. → front-load the first approval, iterate freely after.

---

## Track A — Mobile app-store submission (date-critical)

Ordered so the App Store gates (Apple Sign-In, account deletion) get device
time first. **[F] = founder / portal task, [C] = code.**

### Week of Jul 13 — gates + config cluster
- [ ] **[F] Start the Android closed test** — upload a **functional** `.aab` (also
  initializes Play App Signing), create a Closed testing track, add **12 testers**
  (the Android league friends = the launch cohort), share the opt-in link. *The
  14-day clock is serial and the whole launch's critical path — start this week.
  The build need not be final; update it during the window.*
- [ ] **[F] Apple Sign-In config** (Apple Dev portal: App ID capability, Services
  ID, .p8 key; Firebase Console: enable Apple provider). *Do first — it gates the
  iOS build and needs device testing.* (checklist in chat / PR #489)
- [ ] **[C] Strip temporary push diagnostics** from `app/_layout.tsx`
  (`rnfb open (diag)`, `tapped (diag)`, `flush check (diag)`).
- [ ] **[C] `PrivacyInfo.xcprivacy`** app-level privacy manifest (Firebase, Sentry,
  any accessed-API reasons).
- [ ] **[C] Version → `1.0.0`; align Android `versionCode`** (currently 2 vs iOS 35).
- [ ] **EAS build #1** → device-test **Apple Sign-In + account deletion end-to-end**
  (and opportunistically the deep-link tap).

### Week of Jul 20 — feature completeness
- [ ] **[C] Password-reset flow** (dead "Forgot password?" link in `(auth)/sign-in.tsx`)
  — Firebase `sendPasswordResetEmail`.
- [ ] **[C] Notification preferences** — wire the profile stub to real per-category
  prefs (in scope for v1; backend `UserNotificationPreferences` already exists).
- [ ] **[F] Google Play Console**: content-rating questionnaire, data-safety form
  (Firebase Auth + FCM), privacy-policy URL (Play App Signing initialized with the
  closed-test upload above).
- [ ] **[C] Polished iPad / tablet layout** (in scope for v1) — responsive
  max-width content, multi-column where it earns its keep; after #488 lands.
- [ ] Fix issues found in device testing.

### Week of Jul 27 — polish + release candidate
- [ ] **[C] `CURRENT_YEAR`** hardcode (`2025`) → dynamic once 2026 season data is live.
- [ ] Copy / empty-states / error-message pass.
- [ ] **RC build** → TestFlight + internal Android track; full smoke test.

### Week of Aug 3 — freeze + submit
- [ ] **Code freeze (~Aug 6)**; final production builds (Aug 7).
- [ ] Regression pass on the production build.
- [ ] **Submit to Apple + Google (~Aug 11).**

### Aug 11 → Aug 22 — review
- [ ] Respond to any review feedback / rejections promptly.
- [ ] **Live on both stores by ~Aug 22.**

### Aug 22 → Sept 5 — live pre-season
- [ ] Monitor Sentry / crash-free rate; hotfix JS-only issues via **EAS OTA**
  (no re-review) where possible.

---

## Track B — Backend live-pipeline readiness (kickoff-critical, NOT store-gated)

Can land closer to kickoff than the mobile binary. Target **done by Aug 29**
(before NCAAFB Sept 5); NFL-specific by **Sept 3** (before NFL Sept 10).

- [ ] **Live-finalization reliability**: stream-cancellation race → games unfinalized
  on pod churn; status→enrichment **stale-score** race; 5-hour stream cap.
  (`CompetitionStreamerBase`, `EventCompetitionStatusProcessorBase`.)
- [ ] **NCAA `AthleteCareerStatistics`** processor gap (NCAA athletes spawn it but
  only NFL is registered → DLQ/retries) — add NCAA processor or guard the spawn.
- [ ] **Late-odds → pick re-score** (`FootballContestEnrichmentProcessor`) — leaderboard
  correctness when a higher-priority sportsbook lands after finalization.
- [ ] **Infra HA before kickoff**: Postgres HA (CloudNativePG), NCAA RabbitMQ →
  quorum queues + replicas=3.
- [ ] **NFL onboarding — VERIFY** (founder ~99% sure it's already in place incl.
  historical data): confirm processors attributed, config/broker/DBs/deployments
  live, current-season sourcing running. Treat as verification, not build, until proven otherwise.
- [ ] Lower priority: NetPunt metric stub, situation edge cases, odds observability.

---

## Assumptions / risks
- Season dates: NCAAFB **2026-09-05**, NFL **2026-09-10** (founder-provided).
- Biggest schedule risk: **Google Play first-review latency** on a new dev account.
  Mitigation: submit Aug 11, don't gate on Apple.
- Apple Sign-In portal/Firebase config is founder-side with its own lead time
  (key creation/propagation) — start Week 1.
- Deep-link tap-to-preview is **not** a launch blocker (invites still work in-app);
  verification deferred, ships as-is / fix in a v1.1 OTA if needed.
- EAS free tier build queue ~20–30 min; a paid tier is a later call.

## Resolved decisions (2026-07-11)
- **Submission:** pulled earlier for buffer — submit ~Aug 4, live ~Aug 14.
- **Both platforms launch-critical; Android is primary** (most league friends are on
  Android). Not a fast-follow — both live by kickoff.
- **Google gate = critical path:** 12-tester / 14-day closed test; start this week.
- **Notification preferences:** in scope for v1 (wire real per-category prefs).
- **iPad/tablet:** polished responsive layout in scope for v1 (not just phone-style).
- **No LLC / org account** — use the 12 league-friend testers.

## Still open
- Confirm the 12-tester roster is opted in and the closed test has started.
- NFL live-readiness: verify it's actually in place (founder ~99% sure).
