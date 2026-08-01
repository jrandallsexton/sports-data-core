# Mobile Parity: Join Policy + Public League Discovery

Status: **Phase 1 (find & join) shipped #584; Phase 2 (create-side) not started**
Surfaces: sd-mobile (client only — the BE and API are done)
Web precedent: `docs/features/league-join-policy-and-discovery.md` (#576, #577, #579)

## Why this doc exists

The join-policy + discovery feature set shipped web-first and was deliberately
held from mobile through a production soak, so UX changes were paid once, not
twice (the v2 revision came out of that soak). The web design is now settled;
this doc pins the shared contracts BEFORE mobile screens are written, so the
two apps can't drift on the parts that must match.

## The gap (verified against the mobile tree, 2026-08-01)

Mobile references **none** of this feature set. The BE is platform-agnostic
and already live — every endpoint below exists. This is pure client work.

| Web surface (PR) | Mobile today | Parity work |
|---|---|---|
| Home "Leagues you can join" rail (#577) | ~~no Tier 3~~ **DONE #584** | `JoinableLeaguesCard` + `getPublicLeagues` |
| Public discovery / browse (#576) | ~~no browse screen~~ **DONE #584** | `app/league/discover.tsx` |
| Join confirmation dialog (#577) | ~~raw Join button~~ **DONE #584** | `JoinLeagueConfirmSheet` |
| Closes countdown / closed state (#577) | ~~no joinability awareness~~ **DONE #584** | `JoinClosesLabel` + invite closed-state |
| Commissioner join policy at creation (#576) | `create-league.tsx` sends neither `joinPolicy` nor `leagueWindow` | **Phase 2** — Open vs Locked-at-kickoff choice |
| Week Range creation (#579) | No phase-aware week picker | **Phase 2** — calendar-driven picker |

## Shared contracts mobile MUST mirror (do not re-derive)

These are settled on web; mobile matches them exactly rather than inventing.

- **JoinPolicy** enum: `Open` | `CloseAtFirstGame`. Absent on create ⇒ `Open`
  server-side. Commissioner INPUT, not a lifecycle authority.
- **LeagueWindow** enum: `FullSeason` | `WeekRange` | `DateRange`. Captured
  explicitly at creation — never inferred from date null-ness.
- **`closesAtUtc` / `isJoinable`** on the DTOs are the read-side truth. The
  join gate is the enforcement boundary; the client only shapes UI.
- **Countdown window: 10 days.** Inside 10 days → live countdown
  ("Closes in 2d 4h"); beyond → plain date; past/`isJoinable:false` →
  "Closed". Web: `JoinClosesLabel`.
- **Join confirmation shows details before committing** (joining has no
  self-serve undo): sport/season, pick type + confidence, tiebreaker,
  drop weeks, window, members, commissioner, closes-countdown. Web:
  `JoinLeagueConfirmDialog`.
- **CTA copy is "Join"** everywhere; the browse/rail Join opens the
  confirmation, which then routes through the SAME join path invite links use.
- **`memberCount` / `isMember`** — never derive counts from `members.length`
  (the roster is withheld from non-members).

## API surface (all live)

- `GET /ui/leagues/discover` → fattened `PublicLeagueDto[]` (sport, league,
  seasonYear, memberCount, joinPolicy, closesAtUtc, isJoinable, window,
  tiebreaker, dropLowWeeks).
- `GET /ui/leagues/{id}` → tiered `LeagueDetailDto` (settings + memberCount to
  non-members; roster withheld; carries joinPolicy/closesAtUtc/isJoinable).
- `POST /ui/leagues/{id}/join` (via the mobile join path already used by the
  invite screen).
- `GET /ui/leagues/{sport}/{league}/season-weeks` → labeled week list for the
  Week Range picker (phase-carrying labels, real UTC boundaries).
- Create endpoints already accept optional `joinPolicy`, `leagueWindow`, and
  week-derived `startsOn`/`endsOn`.

## Proposed phasing (EAS batches, not dribs)

**Phase 1 — Find & join a public league** — SHIPPED (#584). One coherent slice:
1. `getPublicLeagues` mobile client + a `useJoinableLeagues` hook.
2. `JoinableLeaguesCard` home rail (Tier 3), rendering null when nothing
   joinable (mirrors web).
3. Public browse screen (full list) reachable from the rail.
4. `JoinLeagueConfirmDialog` (mobile sheet) + `JoinClosesLabel` behavior;
   invite-preview screen gains the closed-state.

**Phase 2 — Commissioner create parity.**
5. Join-policy choice in `create-league.tsx` (Open / Locked-at-kickoff).
6. Week Range picker (phase-aware, calendar-driven) + drop-week ceiling.

Phase 1 lands the landing-page difference the operator noticed and the full
join loop; Phase 2 is create-side plumbing that skews weekday/web and can wait.

## Out of scope

- Admin creation-gate bypass (#579) — operator-only, no mobile user need.
- Any BE change — the contract is frozen; mobile adapts to it.
