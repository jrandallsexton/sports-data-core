# Import picks from one league into another

**Status:** Scoping complete — all design decisions resolved (2026-07-14).
Awaiting sign-off to firm into an implementation plan / start building v1.
**Owner:** Randall.
**Scope:** `SportsData.Api` (picks). Web + mobile UI later.

## Motivation

1. **Product differentiator.** Users in multiple leagues repeatedly pick the same
   games. Copying picks across leagues is a feature specifically called out as
   **missing from other pick'em providers** — real user value, not just a
   convenience.
2. **Near-term testing.** Seed one-off test leagues (a single day, a single week)
   from the season-long "real" league so friends can generate lots of leagues and
   picks with little effort — the more leagues/picks processing, the better the
   pre-launch testing (see [[project_mobile_first_for_mlb_beta]]).

The schema is already primed: `PickemGroupUserPick.ImportedFromPickId (Guid?)`
exists for source-pick traceability. **No migration needed for v1.**

## Scope

- **SU and ATS leagues only.** Over/Under-only leagues are **not a supported
  product**, so O/U is out of scope for this feature entirely.
- **v1 imports same-type only:** `AgainstTheSpread → AgainstTheSpread` and
  `StraightUp → StraightUp`. Cross-type mapping (SU↔ATS) is **deferred** — see
  Deferred.

## How picks work (grounded)

- **Pick** (`PickemGroupUserPick`, table `UserPick`): unique on
  `(PickemGroupId, UserId, ContestId)`. The selection is the picked **team**,
  `FranchiseSeasonId (Guid?)`, for both SU and ATS. Plus `ConfidencePoints (int?)`,
  tiebreaker guesses, `PickType`, `Week`, scored fields, `ImportedFromPickId`.
- **PickType is per league** (`PickemGroup.PickType`) — every member picks the
  same type.
- **Spread is locked per league** on `PickemGroupMatchup.HomeSpread` at
  matchup-generation time. The **same `ContestId` has a separate matchup row per
  league**, with potentially **different** spreads. So a pick copies the
  *team selection*; the *spread it's judged against is the target's*.
- **Locking:** `PickemGroupMatchup.IsLocked()` = `StartDateUtc <= now + 5 min`.
- **Create/upsert path:** `POST /ui/picks` → `SubmitPickCommandHandler` upserts on
  `(UserId, PickemGroupId, ContestId)`, validates group + matchup exist and not
  locked, publishes `UserPickMade`. Import reuses this rule set, so imported picks
  are indistinguishable from hand-made ones.
- **Shared contests:** inner-join `PickemGroupMatchup` on `ContestId` between the
  source and target `GroupId`s. `GetUserLeaguesQuery` lists a user's active
  leagues with `PickType` + `UseConfidencePoints` (drives the source picker).

## The model

A pick is just a picked team (`FranchiseSeasonId`). Since it's the **same game, same
franchise** across leagues, the team copies directly: the target pick's
`FranchiseSeasonId` = the source pick's `FranchiseSeasonId` — no cross-league resolution.
The pick is then scored against the **target league's** locked spread (which may
differ). That subtlety is surfaced in the preview.

### Per-contest outcome

The **target league's open matchups define the universe.** For each, look up the
user's source-league pick for the same `ContestId`:

| Target state for this contest | Outcome |
|---|---|
| No existing target pick | **Import** the source selection. |
| Existing pick == source selection | **No-op** (already matches) — no prompt. |
| Existing pick != source selection | **Collision** → user chooses **keep** (existing) or **replace** (with source). |
| No source pick / not shared | Skip (nothing to copy). |
| Target matchup locked/started | Skip (reason: locked). |

So the user is prompted **only** for genuine conflicts — different picks on the
same game. For a fresh one-off test league there are usually none.

## Guardrails

1. **Membership** — user must belong to both leagues; `source != target`.
2. **Same type** — `source.PickType == target.PickType` (v1). The source picker
   only offers same-type leagues; the server rejects a mismatch defensively.
3. **Shared contest only** — import where the target has a matchup for the same
   `ContestId`.
4. **Target must be open** — skip locked/started target matchups.
5. **Selection, not spread** — copy `FranchiseSeasonId`; the pick is scored against the
   target's locked spread. Surfaced in the preview.
6. **Collisions are explicit** — never silently overwrite a differing target
   pick; the user resolves each (with keep-all / replace-all shortcuts).
7. **Confidence points** — only a concern importing *into* a confidence league
   (importing *from* one is fine; we just don't carry the number over). For a
   **confidence-points target**, the import brings the **team selections** into
   the pick sheet but the sheet **cannot be saved until the user assigns a
   confidence value to each** imported pick — a save-gate, so no incomplete picks
   persist (an unranked pick would score 0). For a **non-confidence target**, the
   import commits the teams directly. *Prerequisite:* confidence-required-at-save
   is **not currently enforced** for confidence leagues — that validation lands
   with this feature.
8. **Idempotency** — set `ImportedFromPickId` = the source pick id; re-running is
   an upsert on the unique key.

## UX (top-down)

- **Entry point:** on the **target** league — "Import picks from another league."
- **Source picker:** the user's other active **same-type** leagues that share ≥1
  contest with the target.
- **Preview (dry-run) — required.** Three groups:
  - *Will import* (team selection shown),
  - *Collisions* — differing existing picks, each with a keep/replace choice
    (default **keep**; keep-all / replace-all shortcuts),
  - *Skipped* + reason (locked / already matches / not shared / needs-confidence).
- **Confirm → commit → summary** (imported N, replaced M, skipped K by reason).
- **Confidence-points target** takes a different last step: instead of a direct
  commit, the imported team selections **pre-fill the pick sheet**; the user
  assigns confidence to each, and the normal (confidence-required) save persists
  them — so the import never leaves unranked picks behind.

## API surface

- `POST /ui/leagues/{targetId}/picks/import/preview`
  body `{ sourceLeagueId }` → per-contest plan
  `{ toImport[], collisions[], skipped[] }` (dry-run, no writes).
- `POST /ui/leagues/{targetId}/picks/import`
  body `{ sourceLeagueId, replaceContestIds[] }` → imports the `toImport` set plus
  the collisions the user chose to replace; returns the summary.
- (Optional) `GET /ui/leagues/{targetId}/picks/import/sources` → candidate
  same-type source leagues that share contests, for the picker.

`Result<T>`, `[FromServices]` handlers, membership-gated. No week/date scoping —
the target's open matchups are the universe.

## Backend logic

1. Validate membership (both), `source != target`, `source.PickType ==
   target.PickType`.
2. Load the target's **open** matchups; load the user's **source** picks for those
   `ContestId`s; load the user's **existing target** picks.
3. Per contest → classify (import / no-op / collision / skip+reason) per the
   outcome table.
4. On commit (non-confidence target) → for each import and each replace-approved
   collision, upsert the target pick (copy `FranchiseSeasonId`, `PickType` = target's,
   `Week` = target matchup's, `ImportedFromPickId` = source pick id) via
   `SubmitPickCommandHandler`'s validation/upsert; each publishes `UserPickMade`.
5. Confidence-points target → return the mapped team selections as a draft for the
   pick sheet rather than persisting; the user assigns confidence and saves via
   the normal (confidence-required) path. Add confidence-required-at-save
   enforcement for confidence leagues as part of this work.

## Phasing

- **v1** — same-type (SU/ATS) import; preview + per-collision resolution;
  membership-gated. No migration. *The testing workhorse.*
- **Deferred** — cross-type SU↔ATS mapping (with a warning); confidence-league UX
  refinement; tiebreaker-guess copy; a possible "mirror league" auto-import for
  future weeks.

## Decisions (all resolved 2026-07-14)

- **Scope:** SU/ATS only; O/U out of scope (no O/U-only leagues as a product).
- **v1:** same-type only (ATS→ATS, SU→SU); cross-type SU↔ATS deferred.
- **Collisions:** prompt per differing pick (keep/replace); matches are silent
  no-ops. Batched in the preview (not sequential modals).
- **Confidence target:** import pre-fills the team selections; the pick sheet is
  save-gated until the user assigns confidence to each (adds
  confidence-required-at-save enforcement). Non-confidence target commits directly.
- **Tiebreaker-guess copy:** deferred.

No open decisions remain — ready to firm into an implementation plan on sign-off.
