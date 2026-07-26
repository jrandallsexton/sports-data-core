# Ended-League Picks Header: Results Glance

Status: implemented (PR #563)
Date: 2026-07-26
Surfaces: SportsData.Api, sd-mobile, sd-ui

## Problem

The picks-page header treats an ended league like an active one:

- One ended league shows **"All Picks Made"** — pick progress is irrelevant once
  the league is over.
- Another shows **"24/46"** — same problem; nobody is finishing those picks.

(Screenshots: `src/about/public/gallery/Screenshot 2026-07-26 105305.png`,
`Screenshot 2026-07-26 105255.png`.)

For an ended league the user's question is "how did I do?", not "how far along
am I?". Replace the progress slot with a results glance:

```text
X | Y | Z
```

- **X** — matchups that produced no scored pick (unpicked + picks whose game
  never resolved, e.g. canceled)
- **Y** — correct picks
- **Z** — incorrect picks

Invariant: **X + Y + Z = total matchups for the week**, so the glance always
adds up. This is why no-result picks fold into X rather than being excluded
(X+Y+Z < total reads as a bug) or counted as incorrect (punishes the user for a
canceled game).

## Decisions (grilled 2026-07-26)

| Question | Decision |
|---|---|
| Client-side compute vs API change | **API change.** Y/Z are technically derivable client-side from `IsCorrect`, but the endpoint returning a raw `List<UserPickDto>` is an API-shape defect we want fixed regardless; the feature justifies touching the contract. |
| Rollout of the breaking change | **Coordinated break.** API + web in one PR/deploy; mobile EAS update immediately after. Installed mobile builds see a broken picks page until the OTA lands — acceptable at beta scale. |
| Where do never-resolved picks land | **Fold into X** ("no result"), preserving X+Y+Z = total. |
| Envelope machinery | **None invented.** Bespoke `UserPicksResultDto` returned as `Result<UserPicksResultDto>` through the existing `ResultExtensions.ToActionResult()` mapping — same as every other endpoint. The deferred global `Response<T>` initiative is untouched. |

## API change

### Endpoint

`GET /ui/picks/{groupId}/week/{week}` (`PicksController.GetUserPicksByGroupAndWeek`)

**Before:** `200 OK` → `List<UserPickDto>` (raw array — the defect)
**After:** `200 OK` → `UserPicksResultDto`

### DTO

```csharp
// Application/UI/Picks/Dtos/UserPicksResultDto.cs
public record UserPicksResultDto
{
    public List<UserPickDto> Picks { get; init; } = [];

    /// Total matchups in this group-week (from PickemGroupMatchup), regardless
    /// of whether the user picked them.
    public int TotalMatchups { get; init; }

    /// Picks with IsCorrect == true.
    public int CorrectCount { get; init; }

    /// Picks with IsCorrect == false.
    public int IncorrectCount { get; init; }
}
```

`X = TotalMatchups - CorrectCount - IncorrectCount` is derived client-side.
The server sends the minimal orthogonal set; shipping a fourth redundant
counter invites the three-plus-one drifting out of agreement.

`UserPickDto` itself is unchanged.

### Handler

`GetUserPicksByGroupAndWeekQueryHandler` gains one indexed count alongside the
existing picks query (`.AsNoTracking()`, same as current):

```csharp
var totalMatchups = await _dataContext.PickemGroupMatchups
    .AsNoTracking()
    .CountAsync(m => m.GroupId == query.GroupId && m.SeasonWeek == query.WeekNumber,
        cancellationToken);
```

Covered by the existing `(GroupId, SeasonYear, SeasonWeek)` index. Correct and
incorrect counts are computed from the already-materialized picks list — no
extra query. Return type becomes `Result<UserPicksResultDto>`; controller
signature becomes `ActionResult<UserPicksResultDto>`; everything else flows
through `ToActionResult()` unchanged.

Note: counts are computed over the **current user's** picks only (the query is
already user-scoped). `User`/`IsSynthetic` fields on `UserPickDto` are
unaffected.

## Client changes

### Mobile (`sd-mobile`) — build first, per direction

1. **`src/services/api/picksApi.ts`** — `getByLeagueAndWeek` return type:
   `UserPick[]` → `UserPicksResult { picks, totalMatchups, correctCount, incorrectCount }`.
2. **`app/(tabs)/picks.tsx`** — consumers of the response switch to `.picks`.
   Header (`headerRight` effect):
   - `isReadOnly === false`: unchanged ("All Picks Made" / `made/total` + Hide
     Picked).
   - `isReadOnly === true`: replace the progress slot with the glance —
     `X | Y | Z` where X renders muted, Y in the success/tint color, Z in the
     error color. ENDED badge and pick-mode badge stay as-is.
   - Counts come from the DTO, not client math over entries.

### Web (`sd-ui`) — same PR (it breaks otherwise)

1. **`src/api/picksApi.js`** — no change (thin axios wrapper).
2. **`src/components/picks/PicksPage.jsx` (~line 400)** — parse the new shape
   (`response.data.picks`). **Required** or web breaks on deploy.
3. Web's own ended-league header glance: same treatment where PicksPage renders
   its read-only header. Kept in scope since the file is already open, but the
   parsing fix is the mandatory part.

## Rollout

Single PR (monorepo): API + web + mobile. Web deploys with the API, so the
array→object break never has a window on web. Mobile:

1. Merge + deploy API/web.
2. Publish EAS update immediately after.
3. Installed mobile builds are broken on the picks page until they pull the
   OTA update — accepted (beta scale, friends-only).

Branch: off latest `origin/main` (not the #561 branch). Separate PR from #561.

## Out of scope

- The global `Response<T>` envelope initiative (`project_api_response_envelope`)
  — this feature neither starts nor depends on it.
- Other endpoints returning raw collections.
- The as-of-week point-in-time bug on past-league picks (records/rankings show
  end-of-season values) — tracked separately.
- Any change to pick submission or scoring.

## Verification checklist

- API: `dotnet build` + `dotnet test` on SportsData.Api (+ new handler test
  covering the counts: correct/incorrect/no-result/unpicked mix).
- Mobile: `tsc --noEmit`, `jest`; manual pass on an ended league (glance shows,
  X+Y+Z equals the week's matchup count) and an active league (unchanged).
- Web: `eslint`, `vitest`; PicksPage renders both active and ended leagues
  against the new response shape.
