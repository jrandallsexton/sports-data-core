# Pick-deadline reminders v2 — per-kickoff, missing-pick-only

**Status:** design approved 2026-09-05. Operator decisions: lead = 60 min
configurable via AppConfig; aggregation = one notification per league per
wave (global aggregate deferred to v3); forward-looking only.

## Why v1 is wrong

v1 (#470, hardened #724/#725/#726) schedules ONE reminder per league-week at
MIN(StartDateUtc) − 60min, per member, with a generic "picks due soon" body —
and never checks whether the member has picked.

The actual lock rule is **per game**: `PickemGroupMatchupExtensions.IsStartLocked`
locks each matchup at its own kickoff − 5 minutes. A league playing the full
NCAA slate has picks locking Thursday night, Friday, Saturday noon/afternoon/
evening — one Thursday-morning reminder covers none of that usefully, and
reminding users who already picked is noise.

## Requirements (operator, 2026-09-05)

1. Remind ~60 minutes (configurable; maybe 30) before **each kickoff wave**,
   not once per week.
2. Only about picks the user has **not** made. Pick exists → send nothing.
3. Exactly one missing pick locking → a **specific** notification naming the
   matchup ("Idaho Vandals at Utah Utes").
4. Multiple picks locking → a **single** notification with the count. Never
   one push per game.

## Design

### Wave model

A *wave* is a cluster of kickoffs within a league-week. Scheduling sorts the
week's distinct `StartDateUtc` values; a kickoff joins the current wave when it
is within `CoalesceWindow` (default 30 min) of the wave's **anchor** (earliest
kickoff in the wave); otherwise it starts a new wave. One reminder fires per
wave at `anchor − Lead` (default 60 min).

- Thursday 19:30 game alone → 1 wave → the "specific" case when unpicked.
- Saturday 16:00/16:15/16:30 stagger → 1 wave, 1 notification, count of 3.

Both knobs come from AppConfig (`NotificationConfig:PickDeadlineLeadMinutes`,
`...:PickDeadlineCoalesceMinutes`); the current hardcoded lead const goes away.

### Scheduling (PickDeadlineReminderScheduler)

`EvaluateAndScheduleForLeagueWeekAsync` keeps its trigger surface (matchup
created/backfilled, start-time updated, prefs updated) but re-derives the
week's **wave set** instead of a single deadline:

- One `PendingScheduledJob` + Hangfire delayed job **per (member, wave)** —
  reusing the existing per-user reschedule / stale-fire / crash-safe machinery
  unchanged. ~30 members × ~5 waves = 150 delayed jobs per league-week;
  Hangfire-cheap. (A per-wave fan-out job is a future optimization, not v2.)
- `PendingScheduledJob` natural key gains the wave anchor: new column
  `WaveAnchorUtc` (non-null for JobKind=PickDeadline v2); lookup key becomes
  (UserId, JobKind, TargetId, SeasonWeek, WaveAnchorUtc).
- Re-evaluation deletes rows whose anchor no longer exists in the derived set
  (v1 "leaves them alone" gap closed); moved kickoffs reschedule their wave.
- Waves whose fire time is already past are skipped (unchanged semantic).

### Dispatch (SendPickDeadlineReminderCommandHandler)

Dispatch was vertically sliced out of the fat NotificationDispatcher during
this build (operator call): each reminder lives in its own handler under
`Application/Reminders/Commands/*`, with the shared pieces (StaleFireGuard,
PushDeviceFanout, ReminderCorrelation) beside them. NotificationDispatcher
was DELETED outright (operator call, over a season-long compat shim):
Hangfire delayed jobs serialize the type they were scheduled against, so
in-flight pre-refactor jobs can no longer bind — the rollout handles them
by bulk-deleting Scheduled jobs and rebuilding everything via the backfill.

The handler takes the wave anchor. Gates, in order (existing gates
unchanged): claim-table dedupe → stale-fire → prefs → **missing-pick
gate** → devices.

Missing-pick gate: wave matchups = league matchups with
`StartDateUtc ∈ [anchor, anchor + CoalesceWindow]`; unpicked = wave matchups
minus the user's `UserPicks` rows (projection kept fresh by
`UserPickMadeConsumer`). Evaluated at fire time, so picks made between
scheduling and fire correctly suppress.

- unpicked = 0 → finalize claim `Suppressed_AllPicked`, no push.
- unpicked = 1 → "Your pick for {Headline} ({league}) locks in about an hour."
- unpicked = N → "{N} picks in {league} lock in about an hour."
- Headline null on the single row → fall back to the count wording.

Claim dedupe already keys on fire-time ticks, so per-wave claims are unique
with no schema change to `NotificationPickDeadline`.

### Headline plumbing (the one data gap)

Notification's `PickemGroupMatchup` projection has no team names. API's
canonical row has `Headline` ("Away at Home").

- Add `string? Headline` to `PickemGroupMatchupCreated` and
  `PickemGroupMatchupDataPublished` (additive record fields — old messages
  deserialize with null; wire-compatible).
- API publishers (matchup creation path + `PickemGroupMatchupsRequestedConsumer`
  backfill responder) populate it from the canonical row.
- Projection gains `Headline` (nullable, MaxLength matching canonical — pin
  the length in a test; InMemory is blind to it).
- The DataPublished consumer's `changed` comparison adds Headline, so the
  operator backfill populates it on existing rows.

### Rollout

1. Merge → deploy. EF migration adds `WaveAnchorUtc` + `Headline` and
   **purges ALL `PendingScheduledJob` rows** — the old rows either describe
   the dead week-level shape (PickDeadline) or point at Hangfire jobs bound
   to the deleted NotificationDispatcher (ContestStart).
2. **Immediately after the pods roll**: bulk-delete every job on the
   Scheduled tab of Notification's Hangfire dashboard
   (jobs.sportdeets.com). They can no longer bind and would only land in
   Failed. Small race: a steady-state event between this delete and step 3
   can schedule a row whose job just got deleted — the backfill will no-op
   on it (row matches fire time). Keep the gap between 2 and 3 short.
3. Run the admin backfill per football sport: re-projects (now with
   Headline) and, via unconditional scheduler evaluation, rebuilds BOTH
   reminder kinds — v2 pick-deadline waves and per-contest start reminders
   — with fresh jobs bound to the slice handlers.
4. Verify in Seq: wave derivation logs, then per-wave fires with
   Sent / Suppressed_AllPicked audit rows in `NotificationPickDeadline`.

## Open decisions (operator)

1. **Lead time**: 60 min default (configurable) — confirm, or 30?
2. **Cross-league aggregation**: v2 sends one notification per league per
   wave (league name in the body is the value). A user in 3 leagues gets 3
   pushes per wave. Global aggregate ("picks locking in 3 leagues") is a
   possible v3; per-league recommended for v2.
3. **Already-locked unpicked games** are never mentioned — reminders are
   forward-looking only. Confirm.
