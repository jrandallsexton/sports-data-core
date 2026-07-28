# UserOption: Per-User Options + Gambling-Content Visibility

Status: implemented
Date: 2026-07-28
Surfaces: SportsData.Api (entity + migration + endpoints), sd-ui, sd-mobile

## Problem and rationale

Some users should not be shown gambling-related content — recovering from
addiction, religious conviction, or simple preference. Straight-Up leagues
display spreads and over/unders as informational flavor even though nothing in
SU play requires them. We want an inclusive default: gambling content appears
only where the game functionally requires it, or where the user has said they
want it. This is the first of an expected family of per-user options, so the
storage must extend without ceremony.

## Decisions (grilled 2026-07-28)

| Question | Decision |
|---|---|
| Storage shape | **Key/value rows + typed DTO.** `UserOption` = (UserId, Key, Value) rows; the API projects known keys into a typed `UserOptionsDto`. Adding an option = registry entry + DTO field — never a migration. Unknown/stale keys ignored. |
| First option semantics | **`ShowGamblingContent`, default OFF.** Render rule: gambling info shows iff the league's pick type requires it (ATS, O/U — lines are the game) OR the user opted in. Per-user only — never a league setting. **Deliberate behavior change:** existing SU-league users stop seeing spreads until they opt in. |
| Enforcement | **Client-side hide.** Display preference, not a security boundary; keeps responses cacheable. One shared predicate per client. |
| Edit surface | **Settings/Profile toggle.** Web: new "Content" section on `/app/settings` (settings-row idiom). Mobile: matching section on Profile. Endpoints mirror notification-preferences. |

## API

### Entity + migration

```csharp
// Infrastructure/Data/Entities/UserOption.cs
public class UserOption : CanonicalEntityBase<Guid>
{
    public Guid UserId { get; set; }
    public string Key { get; set; } = null!;   // max 64
    public string Value { get; set; } = null!; // max 256
    // Unique index (UserId, Key). FK -> User, cascade delete
    // (account deletion's anonymize-in-place keeps the row harmless:
    // options carry no PII — but UserDeleted purge SHOULD remove them;
    // see Open items).
}
```

One EF migration adds the table. Per repo rule: **validate the migration
locally before pushing** (apply against local PG, run the API, exercise the
endpoints).

### Known-keys registry + DTO

```csharp
// Application/User/UserOptionKeys.cs — single source of truth
public static class UserOptionKeys
{
    public const string ShowGamblingContent = "ShowGamblingContent";
}

// Application/User/Dtos/UserOptionsDto.cs — typed projection
public record UserOptionsDto
{
    /// Default false: gambling info renders only where the league's pick
    /// type requires it (ATS/OU) until the user opts in.
    public bool ShowGamblingContent { get; init; } // = false
}
```

The Get handler reads the user's rows, projects known keys (bool parse,
default on absence/garbage); the Update handler upserts rows for the keys
present in the DTO (full-replacement PATCH of known options, mirroring
notification-preferences' race-guard pattern). Unknown rows are left alone —
forward-compatible with options added by newer clients.

### Endpoints (UserController, mirroring notification-preferences)

- `GET /ui/user/me/options` → `UserOptionsDto` (200; defaults when no rows)
- `PATCH /ui/user/me/options` body `UserOptionsDto` → 204

No cross-service projection (unlike notification prefs): options are consumed
by clients only.

## Client rule — one predicate, both platforms

```ts
// shouldShowGambling(pickType, options)
// ATS / OverUnder: lines are the game — always show.
// StraightUp (or unknown): only when the user opted in.
pickType === "AgainstTheSpread" || pickType === "OverUnder"
  ? true
  : options?.showGamblingContent === true;
```

Fetched once per session via React Query (`['user','me','options']`,
staleTime generous — it changes only from the settings screen; invalidate on
PATCH).

### Render sites to gate (verified inventory)

- **Web**: `BettingDisplays.jsx` (primary), `MatchupCard.jsx`,
  `GameStatus.jsx`, `FinalScoreResult.jsx`, `MatchupGrid.jsx`,
  `PicksPage.jsx` (pass-through of pickType/options where needed)
- **Mobile**: `MatchupCard.tsx`, `GameStatus.tsx`, `FinalScoreResult.tsx`

FinalScoreResult nuance: in an ATS/OU league the cover/O-U result IS the pick
result — always shown. In SU leagues any spread-result adornment follows the
predicate.

### Edit surfaces

- **Web `/app/settings`**: new "Content" section (settings-row idiom): switch
  "Show gambling content" with hint "Spreads, totals, and odds in leagues
  that don't require them". aria-live save feedback like the others.
- **Mobile Profile**: matching "Content" section with a Switch, same
  optimistic PATCH + revert pattern as notifications settings.

## Rollout

- Additive API (new table + endpoints) — deploy in any order.
- The default-off flip changes SU-league display for existing users the
  moment clients ship. Release note for beta testers recommended.
- Mobile change is OTA-able (pure JS).

## Open items

- `UserDeleted` purge: add UserOption rows to the Notification/API-side
  deletion path so deleted accounts leave no orphan rows (harmless but
  untidy; the rows carry no PII).
- Future options ride the same registry + DTO; if the option count grows a
  dedicated preferences screen replaces the settings section (deferred).
- **Kids / under-13 mode (idea, 2026-07-28):** a future policy layer could
  force gambling content hidden regardless of the user option or league type,
  making the app openable to younger fans in a wholesome mode. This is why
  ALL surfaces must go through the single `shouldShowGambling` predicate and
  never check the raw option — kids-mode then becomes a one-function change.
  (The real lift there is COPPA; the privacy policy currently disclaims
  under-13 use.)
- Prediction-market surfaces (Polymarket/Kalshi) are gated by this same
  option when they arrive — that was the backlog trigger for this feature.

## Out of scope

- Server-side stripping of gambling fields.
- Per-league or league-configurable visibility (explicitly rejected).
- ATS/O-U league behavior (unchanged by design).
