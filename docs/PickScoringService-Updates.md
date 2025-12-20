# PickScoringService Updates - Test Coverage & Confidence Points

## Summary
Updated `PickScoringService` and its unit tests to:
1. Add comprehensive test coverage for the new `ScoreAgainstSpread` logic
2. Implement confidence points scoring centralized in `ScorePick()` method
3. Ensure both straight-up and against-the-spread picks support confidence points

## Changes Made

### 1. PickScoringService.cs - Refactored Design

**Key Design Decision: Centralized Points Logic**

The scoring logic has been refactored to follow better separation of concerns:

- **Private methods** (`ScoreStraightUp`, `ScoreAgainstSpread`):
  - Only determine **correctness** (`IsCorrect` property)
  - Set `ScoredAt` timestamp
  - Do NOT award points

- **Parent method** (`ScorePick`):
  - Calls appropriate scoring method based on `PickType`
  - Applies **centralized confidence points logic** after scoring
  - Awards points based on `UseConfidencePoints` setting

**Benefits:**
- ? **DRY Principle**: Points awarding logic appears once, not duplicated
- ? **Separation of Concerns**: Scoring determines correctness, parent applies point rules
- ? **Easier Maintenance**: Point rules changes only require updating one location
- ? **Consistent Behavior**: All pick types use identical points calculation

**Confidence Points Logic (Centralized in ScorePick):**
```csharp
// Centralized confidence points logic - applies to all pick types
if (group.UseConfidencePoints)
{
    pick.PointsAwarded = pick.IsCorrect == true ? (pick.ConfidencePoints ?? 0) : 0;
}
else
{
    pick.PointsAwarded = pick.IsCorrect == true ? 1 : 0;
}
```

**Method Signatures:**
```csharp
// Simplified - no useConfidencePoints parameter needed
private void ScoreStraightUp(
    PickemGroupUserPick pick,
    MatchupResult result,
    DateTime now)

private void ScoreAgainstSpread(
    PickemGroupUserPick pick,
    double? spread,
    MatchupResult result,
    DateTime now)
```

### 2. PickScoringServiceTests.cs
**Added comprehensive test coverage for `ScoreAgainstSpread`:**

#### Home Favorite Scenarios:
- ? `ScorePick_AgainstTheSpread_HomeFavorite_CoversSpread_CorrectPick`
  - Home favored by 7, wins 28-17 (covers) ? Correct
- ? `ScorePick_AgainstTheSpread_HomeFavorite_DoesNotCoverSpread_IncorrectPick`
  - Home favored by 7, wins 24-21 (doesn't cover) ? Incorrect
- ? `ScorePick_AgainstTheSpread_Push_HomeFavorite_PickIsIncorrect`
  - Home favored by 7, wins 24-17 exactly (push) ? Incorrect

#### Away Favorite Scenarios:
- ? `ScorePick_AgainstTheSpread_AwayFavorite_CoversSpread_CorrectPick`
  - Away favored by 3.5, wins 27-20 (covers) ? Correct
- ? `ScorePick_AgainstTheSpread_AwayFavorite_DoesNotCoverSpread_IncorrectPick`
  - Away favored by 10, wins 24-17 (doesn't cover) ? Incorrect
- ? `ScorePick_AgainstTheSpread_Push_AwayFavorite_PickIsIncorrect`
  - Away favored by 6, wins 23-17 exactly (push) ? Incorrect

#### Confidence Points with ATS:
- ? `ScorePick_AgainstTheSpread_WithConfidencePoints_CorrectPick_AwardsConfidencePoints`
  - Correct ATS pick with 12 confidence points ? Awards 12 points
- ? `ScorePick_AgainstTheSpread_WithConfidencePoints_IncorrectPick_AwardsZero`
  - Incorrect ATS pick with 12 confidence points ? Awards 0 points

#### Fallback Scenarios:
- ? `ScorePick_AgainstTheSpread_NoSpreadProvided_FallbacksToStraightUp`
  - No spread provided ? Falls back to straight-up scoring
- ? `ScorePick_AgainstTheSpread_ZeroSpread_FallbacksToStraightUp`
  - Zero spread ? Falls back to straight-up scoring

**Added confidence points tests for `ScoreStraightUp`:**
- ? `ScorePick_StraightUp_WithConfidencePoints_CorrectPick_AwardsConfidencePoints`
  - Correct pick with 10 confidence points ? Awards 10 points
- ? `ScorePick_StraightUp_WithConfidencePoints_IncorrectPick_AwardsZero`
  - Incorrect pick with 10 confidence points ? Awards 0 points
- ? `ScorePick_StraightUp_WithConfidencePoints_NullConfidence_AwardsZero`
  - Correct pick with null confidence points ? Awards 0 points (handles missing confidence)

**Updated existing tests:**
- Added `UseConfidencePoints = false` to existing straight-up tests for clarity

## Test Results
? **All 18 tests passing**

```
Total tests: 18
     Passed: 18
     Failed: 0
  Duration: 1.8s
```

## Test Organization
Tests are now organized into regions for better maintainability:
- `#region StraightUp Tests` - 6 tests
- `#region AgainstTheSpread Tests` - 10 tests
- `#region General Tests` - 2 tests

## Key Design Decisions

1. **Centralized Points Logic:** All point awarding happens in `ScorePick()` after determining correctness. This eliminates duplication and makes the code easier to maintain.

2. **Push Handling:** When a spread pick results in a push (exact tie after applying spread), `IsCorrect` is set to `false`. This is intentional as pushes are neither wins nor losses - they're non-events that don't award points.

3. **Confidence Points with Null:** If `UseConfidencePoints` is true but `ConfidencePoints` is null, the pick awards 0 points even if correct. This encourages users to always set confidence points when the feature is enabled.

4. **Fallback Behavior:** When spread is null or zero in ATS mode, the system gracefully falls back to straight-up scoring, maintaining `WasAgainstSpread = true` to track the original pick type.

5. **SetIncorrect Consistency:** The `SetIncorrect` helper method sets `PointsAwarded = 0` directly since incorrect picks always get 0 points regardless of confidence settings.

## Coverage Summary
The `ScoreAgainstSpread` method now has complete test coverage for:
- ? Home favorite scenarios (covers, doesn't cover, push)
- ? Away favorite scenarios (covers, doesn't cover, push)
- ? Confidence points (correct and incorrect)
- ? Fallback scenarios (null spread, zero spread)
- ? Edge cases (missing franchise ID)

The `ScoreStraightUp` method now has complete test coverage for:
- ? Standard scoring (correct and incorrect)
- ? Confidence points (correct, incorrect, null confidence)
- ? Edge cases (missing franchise ID)

## Architecture Notes

The refactored design follows the **Single Responsibility Principle**:

- **Scoring Methods**: Determine correctness based on game outcome
- **Parent Method**: Applies point rules based on league configuration

This makes it trivial to:
- Add new pick types (e.g., OverUnder)
- Change point rules without touching scoring logic
- Test correctness determination independently from point calculation
