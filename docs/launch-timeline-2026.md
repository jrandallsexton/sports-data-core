# 2026 Season Launch Timeline

Status: living plan — dates are targets, adjust as reality moves.
Last updated: 2026-07-11

## Anchors

| Event | Date | Days out (from 2026-07-11) |
|-------|------|-----------|
| **Today** | 2026-07-11 | — |
| **Start Android closed test** (14-day clock) | **ASAP / this week** | 0–5 |
| iOS blockers complete | 2026-08-01 | 21 |
| iOS code freeze + final build | 2026-08-03 | 23 |
| **Submit iOS to Apple** | **~2026-08-04** | **24** |
| Target: **iOS live** | **~2026-08-14** | **34** |
| Android closed test clears (14 days) → apply for production | **~2026-08-01** | 21 |
| Target: **Android live** (fast-follow) | **late-Aug / early-Sept** | ~45–55 |
| **NCAAFB kickoff** | **2026-09-05** | **56** |
| **NFL kickoff** | **2026-09-10** | **61** |

Guiding constraint (founder's ask): live on the stores ~2+ weeks before NCAAFB.
Founder pulled the submission **earlier** for more buffer — sound, because the
first approval is the slow/risky one and everything after is cheap (Apple updates
< 48 h; most fixes ship via **EAS OTA with no review at all** — only native changes
need a new store build).

### Two launch lanes (decoupled)
- **iOS = the launch vehicle.** No tester gate; on-time. Submit ~Aug 4, live ~Aug 14.
- **Android = fast-follow.** The Play account is **personal (post-Nov-2023)**, so
  Google requires a **closed test with ≥20 testers for ≥14 continuous days** before
  production access. This is a serial gate — **start it this week**; Android ships
  when the test + production review clear (late-Aug/early-Sept). If it slips just
  past kickoff, iOS covers the opener. (No LLC / org-account path — 20 testers is
  faster. Testers: pick'em-league friends, family, r/androiddev tester-swap.)

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
- [ ] **[F] Start the Android closed test** — upload the first `.aab` (also
  initializes Play App Signing), create a Closed testing track, add ≥20 testers
  (pick'em friends / family / r/androiddev swap), share the opt-in link. *The
  14-day clock is serial — starting it this week is the single most time-critical
  action.*
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
- **Submission:** pulled earlier for buffer — iOS submit **~Aug 4**, live ~Aug 14.
- **Launch lanes:** iOS on-time; Android fast-follow behind the 14-day closed test.
- **Notification preferences:** in scope for v1 (wire real per-category prefs).
- **iPad/tablet:** polished responsive layout in scope for v1 (not just phone-style).
- **No LLC / org account** — recruit 20 testers instead.

## Still open
- Exact tester roster for the Android closed test (need ≥20).
- NFL live-readiness: verify it's actually in place (founder ~99% sure).
