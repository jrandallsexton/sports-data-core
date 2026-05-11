# Baseball live at-bat header — capture, emission, and UI plan

Working notes for surfacing the current batter and pitcher on the MLB
matchup card's live block (`BaseballGameStatusInProgress`). Builds
directly on PR #310's capture work — that PR persists the resolved
`AtBatAthleteSeasonId` / `PitchingAthleteSeasonId` on
`BaseballCompetitionPlay` and the full participants set on
`CompetitionPlayParticipant`, but `BaseballPlayCompleted` still emits
`null` for both athlete IDs and ships no display fields. This effort
wires the captured IDs onto the event, fetches the display shape on
publish (and on replay), and renders an at-bat header row on the card.

> **UI shape first.** Each side of the new header carries: team logo,
> athlete headshot, ShortName, position abbreviation. Same visual
> language as the existing probable-pitcher slot on `TeamRow.jsx`,
> just two slots side-by-side and driven off live event data instead
> of pre-game sourcing. Suppressed entirely when both athlete-season
> IDs are null (start-inning, end-of-inning, end-batterpitcher).

## Current state — what's wired

### Event (`BaseballPlayCompleted`)

```csharp
public record BaseballPlayCompleted(
    Guid ContestId, Guid CompetitionId, Guid PlayId,
    string PlayDescription, int Inning, string HalfInning,
    int AwayScore, int HomeScore, int Balls, int Strikes, int Outs,
    bool RunnerOnFirst, bool RunnerOnSecond, bool RunnerOnThird,
    Guid? AtBatAthleteId,        // ← stale name; entity column was renamed
    Guid? PitchingAthleteId,     // ← stale name; entity column was renamed
    Uri? Ref, Sport Sport, int? SeasonYear,
    Guid CorrelationId, Guid CausationId);
```

`PublishSportPlayCompletedAsync` still hardcodes both athlete fields to
`null` plus the outs/runner fields to 0/false. The entity now carries
the real values; we just don't ship them yet.

### Producer publish path

`BaseballEventCompetitionPlayDocumentProcessor.PublishSportPlayCompletedAsync`
receives the resolved `BaseballCompetitionPlay` already (created or
updated in the same processor pass). The new `AtBatAthleteSeasonId`
and `PitchingAthleteSeasonId` columns are on the play; the
`CompetitionPlayParticipant` rows hold the matching `PositionId`s.
What's missing is the display shape — ShortName, AthletePosition
abbreviation, headshot URI.

### Replay service

`BaseballContestReplayService` reads the same entity and emits the
same event shape, so it has the same gap.

### UI

`BaseballGameStatusInProgress.jsx` renders three optional rows
(inning+count+outs, runners, last-play) plus the score line. No
athlete fields are read today even though they're in props —
`atBatAthleteId` / `pitchingAthleteId` are forwarded through
`ContestUpdatesContext` but used nowhere downstream.

Probable-pitcher rendering on `TeamRow.jsx` is the visual reference
and source of truth for the headshot lookup pattern:

```csharp
HeadshotUrl = p.AthleteSeason.Athlete != null && p.AthleteSeason.Athlete.Images.Any()
    ? p.AthleteSeason.Athlete.Images.OrderBy(i => i.CreatedUtc).First().Uri.ToString()
    : null
```

## Wire shape changes

### `BaseballPlayCompleted` — rename two existing fields, add six

```csharp
//                                  rename
Guid? AtBatAthleteSeasonId,         // was AtBatAthleteId
Guid? PitchingAthleteSeasonId,      // was PitchingAthleteId

//                                  new
string? AtBatShortName,
string? AtBatPositionAbbreviation,
string? AtBatHeadshotUrl,
string? PitchingShortName,
string? PitchingPositionAbbreviation,
string? PitchingHeadshotUrl,
```

The rename is justified: the existing fields are passed `null` today,
nothing reads them on the API or UI side beyond passthrough, and the
new names match the entity columns and the actual referential
semantics (season-scoped, not global athlete). Burns a wire-shape
change once instead of leaving a permanent inconsistency.

All six new fields are nullable to preserve the existing graceful-
degrade behavior on plays with no participants (e.g., `start-inning`)
and on plays where the athlete-season or athlete-image hasn't been
sourced yet.

## Producer changes

### `PublishSportPlayCompletedAsync` — one query, then emit

When at least one of `AtBatAthleteSeasonId` / `PitchingAthleteSeasonId`
is non-null, fetch the display shape in one batched read:

```csharp
var seasonIds = new[] { play.AtBatAthleteSeasonId, play.PitchingAthleteSeasonId }
    .Where(id => id.HasValue)
    .Select(id => id!.Value)
    .ToArray();

var seasonRows = await _dataContext.AthleteSeasons
    .AsNoTracking()
    .Where(s => seasonIds.Contains(s.Id))
    .Select(s => new
    {
        s.Id,
        s.ShortName,
        s.DisplayName,
        HeadshotUrl = s.Athlete != null && s.Athlete.Images.Any()
            ? s.Athlete.Images.OrderBy(i => i.CreatedUtc).First().Uri.ToString()
            : null
    })
    .ToDictionaryAsync(x => x.Id);
```

Position abbreviations come off the participants rows we already
persist:

```csharp
var positionAbbrevByType = await _dataContext
    .Set<BaseballCompetitionPlayParticipant>()
    .AsNoTracking()
    .Where(p => p.CompetitionPlayId == play.Id)
    .Join(_dataContext.AthletePositions,
          p => p.PositionId, pos => pos.Id,
          (p, pos) => new { p.Type, pos.Abbreviation })
    .ToListAsync();
```

Then assemble: `AtBatShortName = batterSeason?.ShortName ?? batterSeason?.DisplayName` (fallback covers the rare missing-ShortName case), `AtBatHeadshotUrl = batterSeason?.HeadshotUrl`,
`AtBatPositionAbbreviation = position-row-where-type=batter.Abbreviation`.
Mirror for pitcher.

### `BaseballContestReplayService`

Replay reads from the entity and currently emits the same default-
filled event. Apply the same hydration pattern; consider extracting a
shared `BaseballPlayCompletedFactory` if the duplication is real —
defer that decision until both paths are written and the duplication
is concrete.

## API / SignalR

`BaseballPlayCompletedHandler` is a thin passthrough — fans the event
to `NotificationHub` clients. The wire shape widens automatically;
no code change. (The handler doesn't enumerate fields.)

## UI

### `ContestUpdatesContext.handleBaseballPlayCompleted`

Eight new fields onto the merged contest record:

```js
atBatAthleteSeasonId: data.atBatAthleteSeasonId,        // renamed
pitchingAthleteSeasonId: data.pitchingAthleteSeasonId,  // renamed
atBatShortName: data.atBatShortName,
atBatPositionAbbreviation: data.atBatPositionAbbreviation,
atBatHeadshotUrl: data.atBatHeadshotUrl,
pitchingShortName: data.pitchingShortName,
pitchingPositionAbbreviation: data.pitchingPositionAbbreviation,
pitchingHeadshotUrl: data.pitchingHeadshotUrl,
```

The existing `atBatAthleteId` / `pitchingAthleteId` lines retire with
the rename.

### `PicksPage` + `AdminBaseballPage` enrichment merge

Add the same eight fields to the `enrichedMatchup` merge with `??`
fallback to canonical matchup data (mirrors the established
`live.field ?? matchup.field` pattern from PR #309).

### `BaseballGameStatusInProgress.jsx` — new row

A new row above the existing inning+count+outs row. Two slots
side-by-side:

```
┌───────────────────────────────────────────────────────────────┐
│ [logo] [hs] G. Henderson  SS  |  T. Rogers  RHP [hs] [logo]   │
│                  Top 3 · 1-2 · 1 out                          │
│                Runners: 1B 3B                                 │
│      Single to right field, Henderson advances to second      │
└───────────────────────────────────────────────────────────────┘
```

**Logo source**: derive from existing matchup data + `halfInning`. Top
means away bats / home pitches, Bottom is the inverse. The
`MatchupCard` already passes `awayLogoUri` / `homeLogoUri` to its
child rendering — extend the existing prop set on
`BaseballGameStatusInProgress` rather than threading two new logo
props through `GameStatus` (logos route via `MatchupCard` already, so
the new props slot in cleanly).

**Slot rendering** (pseudo-JSX):

```jsx
const hasAtBatRow = atBatShortName || pitchingShortName;
// ...
{hasAtBatRow && (
  <span className="live-state-atbat">
    {atBatShortName && (
      <span className="live-state-atbat-slot">
        {batterLogoUri && <img className="live-state-atbat-logo" src={batterLogoUri} alt="" />}
        {atBatHeadshotUrl && <img className="live-state-atbat-headshot" src={atBatHeadshotUrl} alt="" />}
        <span className="live-state-atbat-name">{atBatShortName}</span>
        {atBatPositionAbbreviation && (
          <span className="live-state-atbat-pos">{atBatPositionAbbreviation}</span>
        )}
      </span>
    )}
    {pitchingShortName && (
      <span className="live-state-atbat-slot">/* pitcher mirror */</span>
    )}
  </span>
)}
```

Each individual slot is independently suppressed when its
`ShortName` is null (handles the partial-resolution case where, say,
only the pitcher has been sourced yet).

**CSS**: new `.live-state-atbat`, `.live-state-atbat-slot`,
`.live-state-atbat-logo`, `.live-state-atbat-headshot`,
`.live-state-atbat-name`, `.live-state-atbat-pos` in
`MatchupCard.css`. Tight horizontal layout; small icon-sized
imagery (~16-20px logo, ~24-28px headshot) to keep the block compact.

## Open considerations

1. **AthleteImage may be empty** — many AthleteSeason rows reference
   athletes whose Images collection isn't yet populated. The
   probable-pitcher path already accepts this (the conditional in the
   query returns null). The UI falls back to "no headshot, show
   ShortName only" without complaint. Acceptable.

2. **Position abbreviation source** — per user direction, use
   `AthletePosition.Abbreviation` directly (e.g., "SS", "CF", "P").
   Not deriving "RHP" / "LHP" from the `Pitches` handedness column
   captured on the play in PR #310. Pitcher slot shows "P" today; we
   can revisit deriving handedness later if requested.

3. **Replay-service duplication** — Producer publish and replay
   service both need the same hydration query. Defer extraction
   until both are written; if the duplication is meaningful, factor
   to a `BaseballPlayCompletedFactory.BuildAsync(play, dataContext, command)`
   helper.

4. **Football is out of scope** — no changes to football play
   emission or UI. The shared event surface gives us breathing room
   to do this only on the baseball side.

## Reference paths

- Event: `src/SportsData.Core/Eventing/Events/Contests/Baseball/BaseballPlayCompleted.cs`
- Publisher: `src/SportsData.Producer/Application/Documents/Processors/Providers/Espn/Baseball/BaseballEventCompetitionPlayDocumentProcessor.cs`
- Replay: `src/SportsData.Producer/Application/Contests/BaseballContestReplayService.cs`
- API handler: `src/SportsData.Api/Application/Events/BaseballPlayCompletedHandler.cs`
- React context: `src/UI/sd-ui/src/contexts/ContestUpdatesContext.jsx`
- Live block: `src/UI/sd-ui/src/components/matchups/BaseballGameStatusInProgress.jsx`
- Probable-pitcher reference: `src/UI/sd-ui/src/components/matchups/TeamRow.jsx`
- Probable-pitcher query reference: `src/SportsData.Producer/Application/Contests/Queries/Matchups/GetMatchupsByContestIds/GetMatchupsByContestIdsQueryHandler.GetProbablePitchersAsync`
- Capture foundation: PR #310 + `docs/baseball-live-data-plan.md`
