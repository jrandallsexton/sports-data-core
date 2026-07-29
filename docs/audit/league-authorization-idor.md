# League Authorization: Closing the IDOR Findings

Status: **implemented (Option A)** — see "What shipped" below
Date: 2026-07-29
Addresses: `docs/audit/launch-readiness-2026-07.md` P0-1, P0-2, and P0-4
Still open after this: P0-3 (`OutboxTestController`), P0-5 (PostgreSQL availability), P0-6 (password reset), P0-7 (placeholder content)
Surfaces: SportsData.Api (primary), sd-mobile + sd-ui (one small client change)

## Problem

Every by-group **read** resolves purely from the route GUID. No handler verifies the caller belongs to the league. Possession of a league id therefore grants:

- the full member roster and league settings (`GetLeagueByIdQueryHandler`)
- every member's points and accuracy (`GetLeaderboardQueryHandler` — checks only that the group *exists*)
- the league's private message board (`GetThreadsQueryHandler`, `GetRepliesQueryHandler`)
- matchups, week overview, and scores (`LeagueController` `:217`, `:337`, `:382`)

And two **writes** are equally open: posting to any league's message board (`CreateThreadCommandHandler`, `CreateReplyCommandHandler`) and submitting picks into any league (`SubmitPickCommandHandler`).

League GUIDs are not secret. They appear in invite links, share sheets, screenshots, and logs.

Note the asymmetry that makes this a clear oversight rather than a design choice: the *invite* paths already do this correctly. `SendLeagueInviteCommandHandler:63-71` is the reference implementation —

```csharp
var inviterIsMember = await _dbContext.PickemGroupMembers
    .AsNoTracking()
    .AnyAsync(m => m.PickemGroupId == league.Id && m.UserId == command.InvitedByUserId, ct);
if (!inviterIsMember)
    return new Failure<T>(default!, ResultStatus.Forbid, [new ValidationFailure(..., "Only league members can invite others.")]);
```

`CloneLeague`, `InviteUserToLeague`, and `GetInviteableUsers` do the same. The read paths were simply never given the same treatment.

## The constraint that shapes the design

A blanket members-only guard **breaks two legitimate non-member reads**:

1. **Public-league discovery.** `GetPublicLeaguesQueryHandler` deliberately returns leagues where `IsPublic && !isMember`. A user browsing public leagues must be able to look at one before joining.
2. **The invite preview.** `sd-mobile/app/league-invite/[leagueId].tsx` calls `getLeagueById` **as a non-member of a private league** — that is the entire point of the screen shipped in #570. It renders `name`, `description`, member *count*, and `isPublic`. It does not render individual members.

**And there is no invite record to authorize against.** Verified: `InviteUserToLeagueCommandHandler` only publishes `UserInvitedToPickemGroup` (a push notification) and persists nothing invitee-scoped. `PickemGroupInvitation` exists as an entity but is a revocable *link* token with no invitee field — and is written by **no** Application code at all. So "allow the read if this user was invited" is not implementable today without new persistence.

## Decision required

**Which model for `GET /ui/leagues/{id}`?**

### Option A — Tiered response (recommended)

One endpoint, two shapes. Members get the full `LeagueDetailDto`. Non-members get the same DTO with the **member roster omitted** and a new `MemberCount` populated.

- Preserves public discovery and the invite preview with **no new tables, no new endpoints**.
- The roster — real people's usernames — is the actual privacy payload; league settings are not sensitive (a public league advertises them by design).
- Cost: one DTO field, and a one-line mobile change (`league.members.length` → `league.memberCount`).
- Residual exposure: someone holding a GUID learns a private league's *name, description, settings, and size*. Not nothing, but not the roster.

### Option B — Persist per-user invitations, gate strictly

Add an invitee-scoped invitation record; guard becomes `isMember || IsPublic || hasPendingInvitation`.

- Strictly correct: no capability leaks from GUID possession.
- Cost: new table + migration, rework of the invite flow, and **link-based invites still break** — a shared URL has no invitee, so it would need a signed token. That is a feature, not a fix.
- Recommend deferring this until invites are revisited on their own merits.

### Option C — Guard everything except league detail

Leave `GET {id}` open, lock the rest.

- Least work, keeps both flows.
- Leaves the roster readable by GUID — the single most identifying payload in the set. Not recommended.

**My recommendation: Option A now, Option B later if invites get a proper lifecycle.**

## Implementation plan (assuming Option A)

### 1. Shared guard

New `ILeagueMembershipGuard` in `Application/UI/Leagues/Authorization/`, registered scoped:

```csharp
Task<bool> IsMemberAsync(Guid leagueId, Guid userId, CancellationToken ct);
```

Single indexed `AnyAsync` over `PickemGroupMembers` — the `(GroupId, UserId)` unique index already exists, so this is an index seek, not a scan. One extra round-trip per guarded request; acceptable, and cacheable later if it ever shows up in a profile.

Handlers return `ResultStatus.Forbid` on failure, matching the invite paths' existing convention. **Not** 404: these endpoints already confirm existence to any caller today, so 404-as-concealment would be a false promise while `GET {id}` still returns a preview.

### 2. Handlers to change

| Handler | Change |
|---|---|
| `GetLeagueByIdQueryHandler` | Add `UserId` to the query (it currently carries **only** `LeagueId` — membership checking is structurally impossible). Populate `MemberCount` always; include `Members` only for members. |
| `GetLeaderboardQueryHandler` | Guard → Forbid |
| `GetLeagueWeekMatchupsQueryHandler` | Guard → Forbid |
| `GetLeagueWeekOverviewQueryHandler` | Guard → Forbid |
| `GetLeagueScoresByWeekQueryHandler` | Guard → Forbid |
| `GetThreadsQueryHandler`, `GetRepliesQueryHandler` | Guard → Forbid |
| `CreateThreadCommandHandler`, `CreateReplyCommandHandler` | Guard → Forbid (write) |
| `SubmitPickCommandHandler` | Guard → Forbid (write) |
| `GetUserPicksByGroupAndWeekQueryHandler` | Guard → Forbid. Already caller-scoped so nothing leaks, but a non-member should not transact against a league at all. |

`GenerateLeagueWeekPreviews` also needs `[Authorize]` + a guard — that is audit finding P0-4, folded in here since it is the same file and the same fix.

### 3. Clients

- **sd-mobile**: `league-invite/[leagueId].tsx` → `league.memberCount`; `LeagueDetail` type gains `memberCount`.
- **sd-ui**: `LeagueDetail.jsx` renders the roster for members (unchanged). Non-members reaching `/app/league/{id}` get the count-only line described in "What shipped"; the discovery flow's Join action is unaffected.

### 4. Tests

Per guarded handler: member → success; non-member → `Forbid`. Plus `GetLeagueById` non-member → preview shape (roster empty, count populated) and member → full. Roughly 20 focused unit tests using the existing `ApiTestBase` in-memory pattern.

## What shipped

Option A, as planned above, with three deviations worth recording:

**1. The guard grew two more methods.** The message-board handlers are keyed by
`threadId` / `postId`, not by league, so `IsMemberAsync` alone could not
authorize them. `IsMemberOfThreadGroupAsync` and `IsMemberOfPostGroupAsync`
resolve the owning league and test membership in a **single** joined query
rather than two round-trips. An unknown thread/post id yields no rows, so a
bogus id is denied rather than throwing.

**2. Message-board actions are guarded in `MessageboardController`, not in
their handlers.** `GetThreadsQueryHandler` and `GetRepliesQueryHandler` return
`PageResult<T>`, which has no way to express a failure — there is no
`Result<T>` envelope to put `Forbid` into. Rather than reshape the pagination
contract inside a security fix, all six actions (threads, replies, and the two
reaction endpoints) check at the controller and return `Forbid()`. Worth
revisiting when `PageResult<T>` is folded into the `Response<T>` envelope
that's already on the backlog.

**3. Non-members see a member *count*, not an empty roster.** The plan accepted
"non-members see an empty roster" as correct-but-ugly. In practice both detail
screens render `No members yet.` on an empty list, which is a lie about a
league that has members. Mobile and web now branch on `isMember` and show
`"N members. Join to see who's in it."` — hence the new `IsMember` flag on the
DTO alongside `MemberCount`.

Coverage: 9 tests in `LeagueAuthorizationTests` — the real guard against seeded
rows (member true / stranger false / empty-and-unknown ids false, including the
thread and post variants), the tiered detail response both ways, and
`Forbid` on the guarded reads. Handler tests inherit a permissive guard from
`ApiTestBase` so authorization is asserted in one place instead of smeared
across every suite. Full API suite: 573 tests green.

## Risk and rollout

- **Breaking for any client relying on unguarded reads.** Both first-party clients are covered above; no third-party consumers exist.
- **Deploy order:** API and web together; the mobile `memberCount` change ships via EAS (pure JS). Old mobile builds would render "0 members" on the invite screen until they update — cosmetic, non-blocking. If that is unacceptable, populate `MemberCount` *and* leave a length-only `Members` stub for one release; say so and I will.
- Everything is additive at the DB layer — **no migration**.

## Out of scope

- Invitation lifecycle / signed invite links (Option B).
- The other audit P0s (`OutboxTestController`, password reset, placeholder content, database hardware).
- Admin-gating contest refresh/finalize and preview approve/reject (audit P1) — same theme, separate change.
