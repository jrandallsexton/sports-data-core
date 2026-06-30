# Unique Username Foundation

Status: design (PR-A of the username work)
Last updated: 2026-06-30

## Why

Today `User.DisplayName` does double duty: it's the auto-generated handle
(`clever_clam` via `DisplayNameGenerator`) **and** a user-settable free-text
label, and it is **not unique**. We want to split those jobs:

- **`Username`** — a stable, unique, lowercase handle (`jrandallsexton`) used
  for search, invite-by-username (PR2), and any future @-mention.
- **`DisplayName`** — unchanged role: a mutable, free-text label a user can set
  to whatever they like (`YouWillAllLose`). Stays non-unique.

This PR (PR-A) is the foundation: add `Username`, backfill every existing user
with a unique value, enforce uniqueness, and give users a place to view/edit it.
PR2 (invite-by-username autocomplete) builds on it.

## Model

```text
User (API, canonical):
  Username     string, required, UNIQUE (case-insensitive), 3–30, [a-z0-9_]
  DisplayName  string, required, NON-unique   (unchanged)
```

### Username rules

- Charset `[a-z0-9_]`; length 3–30; stored lowercased.
- **Case-insensitive uniqueness** — `JRandall` and `jrandall` collide. Enforced
  by a unique index on a normalized (lowercased) value; simplest is to store
  `Username` already-lowercased and index it directly.
- Reserved-word blocklist (`admin`, `api`, `support`, `root`, `sportdeets`, …)
  to keep impersonation/route-collision handles out.
- Mutable. (Rate-limiting / username-history is out of scope for PR-A.)

### Username vs DisplayName — surface mapping

- **DisplayName** shows in leaderboards, standings, league-member lists — the
  places that render `m.User.DisplayName` today (no change).
- **Username** is the identity handle: PR2 invite autocomplete, search, future
  @-mentions. Profile screens show both ("@username" + display label).

## Backfill (the load-bearing part)

Existing rows have no `Username` and `DisplayName` collisions exist, so the
unique constraint can't be switched on directly. Three-step migration:

1. Add **nullable** `Username` column (no constraint yet).
2. **Backfill** every user with a unique value (data step, below).
3. Add the **unique index** + flip the column to `NOT NULL`.

### Seed algorithm (per user)

```text
seed = localPart(email)                 // text before '@'
seed = lower(seed)
seed = stripFrom('+', seed)             // drop plus-addressing tag
seed = keep([a-z0-9_], seed)            // strip dots, etc.
if len(seed) < 3:  seed = slug(DisplayName)   // fallback 1
if len(seed) < 3:  seed = "user"               // fallback 2
seed = truncate(seed, 30)
username = seed
n = 2
while taken(username):                  // case-insensitive
    username = truncate(seed, 30 - len(n)) + n
    n++
```

- **Local-part only — never the full email.** The domain is the privacy/spam/
  enumeration risk; the local-part alone can't reconstruct the address. The
  seed is just a default; users rename if they don't want their name public.
- Collisions resolve with the shortest numeric suffix
  (`jrandallsexton`, `jrandallsexton2`, …).
- Deterministic and idempotent: re-running skips users that already have a
  `Username`.

The backfill runs as a **data step validated locally before the PR** (per
project guardrail — never push an unvalidated migration). Implementation choice
captured at build time: an idempotent EF data migration vs. a one-shot admin
command. Leaning toward a data migration so prod gets it on deploy with no
manual step, but it must be proven against a local copy of prod data first.

## New-user flow

`UpsertUserCommandHandler` already generates a `DisplayName` when none is
supplied. It will additionally mint a unique `Username` for brand-new users
using the same seed algorithm, retrying on the unique-constraint race it already
handles for `FirebaseUid`. No new sign-up step — users land with a sensible
default handle and can change it in profile. (A "pick your username" onboarding
prompt is a possible follow-up, not PR-A.)

## Touchpoints

| Area | Files |
|------|-------|
| Entity + config | `src/SportsData.Api/Infrastructure/Data/Entities/User.cs` (add `Username`, unique index) |
| Migration + backfill | new EF migration under `src/SportsData.Api/Migrations/` (nullable → backfill → unique+NOT NULL) |
| New-user mint | `Application/User/Commands/UpsertUser/UpsertUserCommandHandler.cs` + a `UsernameGenerator`/seed helper (sibling to `DisplayNameGenerator`) |
| Edit/validate | new `UpdateUsername` command + validator (charset/length/reserved/uniqueness); `UpsertUserCommandValidator` left as-is for DisplayName |
| Read surface | `Application/User/Dtos/UserDto.cs` + `GetMeQueryHandler` expose `Username` |
| Web | `src/UI/sd-ui/src/components/settings/SettingsPage.jsx` — show/edit username |
| Mobile | `src/UI/sd-mobile/app/(tabs)/profile.tsx` — show/edit username |

PR2 then adds the user-search endpoint + the league-overview autocomplete and
reuses the `UserInvitedToPickemGroup` event/consumer already shipped in PR1.

## Out of scope (PR-A)

- Invite-by-username UI/search (that's PR2).
- Username change history / rate-limiting / cooldowns.
- "Choose your username" onboarding step.
- Propagating `Username` into the Notification projection (not needed until a
  surface there renders handles).
