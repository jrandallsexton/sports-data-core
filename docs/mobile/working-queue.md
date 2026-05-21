# Mobile working queue

A living list of mobile-side work items. Add / remove / promote as we
go. Not a design doc — items here may have separate design docs once
they get picked up.

Order within each section reflects rough priority, but only the
**In flight** section is binding; everything else is just on deck.

Last updated: 2026-05-21

---

## In flight

- EAS Update end-to-end smoke test. Fresh `eas build --profile
  production --platform ios` after #345 (web platform guard) + #346
  (text size) merged. Install via TestFlight, then `eas update
  --branch production --message "test: ota verification"` and confirm
  the OTA lands after two cold launches. See
  `docs/mobile/eas-update.md` for the validation flow.

---

## EAS Update — Phase 2 (unblocks after smoke test passes)

- Update `expo-deployment-model.md` Section 4 — EAS Update is now
  live; remove the "not currently wired up" caveat and cross-link to
  `eas-update.md`.
- Decision-tree table in `expo-deployment-model.md`: "JS-only? OTA.
  Native? Build. Mixed? Build." with examples.
- `npm run ota:prod` + `ota:preview` wrapper scripts so the
  steady-state command is one word.
- (Optional) GitHub Action that publishes an OTA on merge to main.
  Needs EAS access token in repo secrets.
- Smoke-test guide doc — what to publish, what to watch on device,
  how to verify it landed.

---

## Mobile drift items (from MatchupCard blueprint)

- **Postponed / cancelled status handling** — mobile shows the raw
  status string in error color; web falls through to Scheduled
  markup. Pick one and apply on both. See blueprint "Known drift".
- **Dark-mode logo variants** — both platforms have access to
  `homeLogoUriDark` / `awayLogoUriDark`; web swaps via `useTheme()`;
  mobile uses the default URI in both schemes. *MatchupCard
  addressed separately; see follow-ups below for the other
  surfaces.*
- **Dark-mode logos in Contest Overview** (follow-up) — the
  `ContestOverviewDto` returned by `/ui/matchup/{contestId}/overview`
  only carries `logoUrl`, no dark variant. Needs backend DTO
  additions (server-side mapper + entity column lookup) before the
  mobile contest-overview header can swap. Same backend pattern as
  the existing MatchupForPickDto dark fields.
- **Dark-mode logos in Team Card** (follow-up) — `TeamCardDto`
  returned by `/ui/team/{slug}` only carries `logoUrl`. Same
  backend treatment needed.
- **TeamRow expandable schedule** — web's TeamRow opens a per-team
  schedule on tap via `useTeamSchedule`; mobile's TeamRow has no
  equivalent.
- **DeetsMeter / AI prediction bars** — web renders meters inside
  the card; mobile omits the slot entirely.
- **ConfidencePicker integration** — confidence-points leagues work
  on web; mobile has no path to set a confidence score.
- **Headline banner population** — confirm server-side that
  `matchup.headLine` populates consistently across sports + states.
  Today it appears only for some marquee games.

---

## Brand polish

- **`wordmark-on-light.png` bug** — "Deets" renders white-on-white,
  invisible. Asset needs the "Deets" portion in near-black.
  Currently unused in code (live `<Wordmark>` component handles
  light/dark fine), but the asset itself is shipped for external use
  (press kit, share images).
- **Android themed-icon monochrome asset** — currently dropped from
  `app.json` because the supplied asset was a generic chevron, not
  the SD silhouette. Need a real monochrome version of the SD mark
  (transparent background, solid color so Android can tint it for
  themed icons).
- **Asset folder dedup** — six files exist at both top-level and in
  subfolders (`icon.png` vs `expo/icon.png`, etc.). Pick one
  canonical location.

---

## Mobile housekeeping

- **Split `MatchupCard.tsx`** (~800 lines) into per-component files
  (TeamRow, OddsRow, PickButton, PickButtons). Separately, split
  `app/(tabs)/(details)/sport/[sport]/[league]/game/[id].tsx`
  (~1200 lines) — that one's even bigger.
- **Scoped Scheduled MatchupCard whitespace** — see
  `memory/project_scheduled_card_whitespace.md`. The current vertical
  stack leaves a lot of empty right-side space on wide phones; a
  future redesign should use width-conditional layout OR a more
  compact info format. Do NOT re-propose the 2-col-by-default
  approach (already tried + reverted).
- **iPhone 14 real-device readability validation** for the Round 3
  font bumps + the new S/M/L control. Smoke test of how the various
  surfaces hold up at L.
- **`expo-deployment-model.md`** dead-link / stale-section audit.

---

## Android deployment

- Play Console store listing (app name, description, screenshots,
  content rating, data safety form). Tedious — see
  `docs/mobile/android-deployment.md`.
- Internal Testing track configured with tester accounts.
- First `.aab` manual upload (Google requires this for the first
  build; `eas submit` works after).
- Google Cloud service account JSON for automated `eas submit`.
- `eas.json` `submit` section for production Android.

---

## Backend queue (separate from mobile but in our broader queue)

- Hangfire purge + ongoing maintenance CronJob. See
  `memory/project_hangfire_purge.md`.
- Producer role split (Api / Ingest / Worker) — Provider done in
  #178, Producer planned. See `memory/project_role_split.md`.
- Per-role connection pool sizing — needed after the role split to
  stay under PostgreSQL's 500 max_connections.
- API `Response<T>` envelope pattern — replace flat success DTOs.
  See `memory/project_api_response_envelope.md`.
- Fold `CompetitionController` into `ContestController` —
  Producer-side URL surface cleanup.
- `UpsertMatchupPreview` ExecutionStrategy wrap — latent retry bug.
- In-season-but-completed cache bypass policy — completed
  current-season games still re-fetch ESPN.
- SignalR consumer Info logs — downgrade to LogDebug after MLB
  season stabilizes (PR #312).

---

## Environment / infra

- **Node 22 LTS upgrade** — Expo + npm warn that v20.12.2 is
  outdated (`required: >=20.19.4`). Doesn't block anything yet but
  the warnings clutter every CLI invocation.
- **EAS plan upgrade decision** — free tier means ~20–30 min queue
  per native build. Once OTA validates, the urgency of native
  builds drops; revisit whether the paid tier is worth it.

---

## Deferred (don't pick up without authorization)

- **SeasonYear denormalization removal** — plan complete, ~150
  files. Deferred until after historical sourcing settles.
- **Migration runner separation** — extract EF migrations from
  service startup to avoid concurrent pod migration races.

---

## How to use this doc

- **Adding an item:** drop it in the most-fitting section. If it's
  the next thing we'll do, promote it to "In flight."
- **Starting an item:** move to "In flight" with a one-line note on
  what's blocking or in progress.
- **Finishing:** remove from the doc entirely, or move to a
  "Completed" section if the context matters for nearby work.
- **Promotion / demotion:** rearrange within sections — the doc
  reflects rough priority but isn't binding except for "In flight."
- This is NOT a memory or a design doc. Keep entries short. If
  something needs more than two lines of explanation, that's a sign
  it deserves its own doc.
