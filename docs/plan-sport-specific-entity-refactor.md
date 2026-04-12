# Plan: Sport-Specific Entity Refactor

## Status: Deferred — capture now, execute after MLB sourcing stabilizes

## Context

Several canonical entities currently combine football-specific and baseball-specific fields in a single table, with nullable columns for the sport that doesn't use them. This works for initial MLB onboarding but creates schema confusion and wasted columns as more sports are added.

The pattern for sport-specific entities is already proven: `Athlete` → `TeamAthlete` → `FootballAthlete` / `BaseballAthlete` with TPH (table-per-hierarchy) discrimination and sport-specific DbSets.

## Entities to Refactor

### CompetitionPlay (highest priority)

Football and baseball plays are structurally different. Current `CompetitionPlay` is a football play with nullable fields that baseball ignores.

**Shared (CompetitionPlayBase):**
- Id, CompetitionId, SequenceNumber
- Type (id, text), Text, ShortText, AlternativeText, ShortAlternativeText
- AwayScore, HomeScore, PeriodNumber
- ScoringPlay, Priority, ScoreValue
- Modified, Wallclock, Team (FranchiseSeasonId)
- Participants, Probability, ExternalIds

**Football-specific (FootballCompetitionPlay):**
- DriveId (FK to CompetitionDrive)
- StartDown, StartDistance, StartYardLine, StartYardsToEndzone, StartFranchiseSeasonId
- EndDown, EndDistance, EndYardLine, EndYardsToEndzone, EndFranchiseSeasonId
- StatYardage
- ClockValue, ClockDisplayValue
- ScoringType, PointAfterAttempt

**Baseball-specific (BaseballCompetitionPlay):**
- AtBatId — groups pitches/events within a single at-bat
- AtBatPitchNumber — pitch number within the at-bat
- BatOrder — batter's position in the lineup
- BatsType, BatsAbbreviation — batter handedness
- PitchCoordinateX, PitchCoordinateY — strike zone location
- HitCoordinateX, HitCoordinateY — field location where ball was hit
- PitchTypeId, PitchTypeText, PitchTypeAbbreviation — fastball, slider, etc.
- PitchVelocity — mph
- PitchCountBalls, PitchCountStrikes — running count
- ResultCountBalls, ResultCountStrikes — count at play result
- Trajectory — fly ball, ground ball, line drive
- StrikeType — foul, swinging, looking
- SummaryType — I (inning), A (at-bat start), P (pitch), N (result)
- AwayHits, HomeHits — running hit totals
- AwayErrors, HomeErrors — running error totals
- RbiCount — RBIs on this play
- IsDoublePlay, IsTriplePlay — boolean flags

### CompetitionSituation (medium priority)

**Football:** Down, Distance, YardLine, IsRedZone, AwayTimeouts, HomeTimeouts
**Baseball:** Balls, Strikes, Outs, OnFirst/OnSecond/OnThird (athlete refs), Pitcher, Batter

Currently baseball situations store football fields zeroed out. No baseball-specific data is captured.

### Contest (lower priority)

Current `Contest` is fairly sport-agnostic (teams, scores, dates, venue, week). The main difference is:
- Football: SeasonWeekId is meaningful (weekly schedule)
- Baseball: SeasonWeekId is nullable (no weeks in Spring Training, weekly grouping is artificial for regular season)
- Baseball: series tracking (3-game series, season series) — not on the entity today

This may not need a full TPH split — nullable SeasonWeekId already handles the difference.

### Competition (lower priority)

Current `Competition` is fairly generic. The main baseball addition would be:
- Format.regulation.periods = 9 (vs 4 for football) — already handled by variable linescore count
- Series data — current/preseason/season series with win tracking
- Probables — starting pitchers

These might be better as related entities rather than a TPH split.

## Approach

Follow the established pattern:
1. Create base class with shared fields
2. Create sport-specific subclasses with additional fields
3. TPH discrimination via EF Core (single table, Discriminator column)
4. Sport-specific DbSets in FootballDataContext / BaseballDataContext
5. Migration to add Discriminator column and new columns
6. Backfill existing rows with correct discriminator value

## Risk Assessment

**Low risk.** The pattern is proven on Athlete/AthleteSeason. The main work is:
- Creating the entity hierarchy (straightforward)
- Updating processors to use the sport-specific types (already separate processors per sport)
- Migration with data backfill (mechanical — set Discriminator = 'FootballCompetitionPlay' for all existing rows)
- Updating query handlers that read CompetitionPlay (need to handle the base type or sport-specific)

**No architectural risk.** This is a schema refinement, not a design change.

## When to Execute

After:
1. MLB sourcing is stable and producing canonical data
2. Live game streaming is working end-to-end
3. The current nullable-field approach has proven out which fields are truly sport-specific vs shared

The live game streaming work will reveal whether the current shared entity causes real problems or just cosmetic ones. If baseball plays process correctly with the football fields zeroed, the refactor can wait.
