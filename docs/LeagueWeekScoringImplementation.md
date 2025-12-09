# League Week Scoring Implementation Summary

## ? Completed Steps

### 1. Entity Updates
- ? Updated `PickemGroupWeekResult` with:
  - `IsDropWeek` property (bool)
  - `Rank` property (int?)
  - Navigation properties for `Group` and `User`

### 2. Services Created
- ? Created `ILeagueWeekScoringService` interface
- ? Created `LeagueWeekScoringService` implementation with:
  - `ScoreLeagueWeekAsync()` - Scores a single league/week
  - `ScoreAllLeaguesForWeekAsync()` - Scores all leagues for a week
  - `DetermineWeeklyWinnersAsync()` - Marks winners and assigns ranks
  - `CalculateDropWeeksAsync()` - Identifies and marks drop weeks

### 3. Service Updates
- ? Simplified `LeagueService.GetLeagueScoresByWeek()` to read from `PickemGroupWeekResults`
- ? Updated `LeagueScoresByWeekDto` with `IsWeeklyWinner` and `Rank` properties

### 4. Database Migration
- ? Created migration SQL script at `docs/migrations/AddDropWeekAndRankToPickemGroupWeekResult.sql`

---

## ?? Remaining Steps

### 1. Register Service in DI Container

Add to your service registration (likely in `Program.cs` or `Startup.cs`):

```csharp
services.AddScoped<ILeagueWeekScoringService, LeagueWeekScoringService>();
```

### 2. Apply Database Migration

Run the SQL migration against your database:

```sql
-- From docs/migrations/AddDropWeekAndRankToPickemGroupWeekResult.sql
ALTER TABLE [PickemGroupWeekResult]
ADD [IsDropWeek] bit NOT NULL DEFAULT 0;

ALTER TABLE [PickemGroupWeekResult]
ADD [Rank] int NULL;
```

Or if you have EF tools installed:
```bash
dotnet ef database update --project src/SportsData.Api
```

### 3. Integrate with Hangfire

Update your existing Hangfire job that scores picks to call the league scoring service:

```csharp
public class WeeklyScoringJob
{
    private readonly IPickScoringService _pickScoringService;  // Existing
    private readonly ILeagueWeekScoringService _leagueScoringService;  // New
    
    public async Task ScoreWeek(int seasonYear, int weekNumber)
    {
        // 1. Score all picks first (existing logic)
        await _pickScoringService.ScoreAllPicksForWeekAsync(seasonYear, weekNumber);
        
        // 2. Score all leagues for the week (NEW)
        await _leagueScoringService.ScoreAllLeaguesForWeekAsync(seasonYear, weekNumber);
    }
}
```

### 4. Backfill Historical Data (Optional)

If you want to populate data for previous weeks:

```csharp
public async Task BackfillHistoricalWeeks(int seasonYear)
{
    var allWeeks = await _dbContext.PickemGroupMatchups
        .Select(m => m.SeasonWeek)
        .Distinct()
        .OrderBy(w => w)
        .ToListAsync();
    
    foreach (var week in allWeeks)
    {
        await _leagueScoringService.ScoreAllLeaguesForWeekAsync(seasonYear, week);
    }
}
```

### 5. Update AppDataContext (if needed)

Ensure `PickemGroupWeekResults` DbSet exists in your `AppDataContext`:

```csharp
public DbSet<PickemGroupWeekResult> PickemGroupWeekResults { get; set; }
```

And the entity configuration is registered:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfiguration(new PickemGroupWeekResult.EntityConfiguration());
    // ... other configurations
}
```

---

## ?? How It Works

### Weekly Scoring Flow

1. **Hangfire Job Triggers** (after games complete)
   - Existing: `PickScoringService.ScoreAllPicksForWeekAsync()`
   - **New**: `LeagueWeekScoringService.ScoreAllLeaguesForWeekAsync()`

2. **For Each League**:
   - Aggregate user picks into weekly totals
   - Create/update `PickemGroupWeekResult` records
   - Determine weekly winners (handles ties)
   - Calculate drop weeks across all weeks
   - Assign rankings

3. **API Endpoint**:
   - Simply reads from `PickemGroupWeekResults`
   - No calculations on-demand
   - Fast, consistent data

### Data Model

```
PickemGroupWeekResult
??? TotalPoints         (sum of PointsAwarded for the week)
??? CorrectPicks        (count of correct picks)
??? TotalPicks          (count of all picks made)
??? IsWeeklyWinner      (true if tied for first place)
??? IsDropWeek          (true if this is one of user's N lowest weeks)
??? Rank               (1-based ranking for the week)
??? CalculatedUtc       (timestamp of last calculation)
```

---

## ?? Testing Steps

1. **Apply migration** to add columns
2. **Register service** in DI
3. **Run backfill** for current season:
   ```csharp
   await _leagueScoringService.ScoreAllLeaguesForWeekAsync(2024, weekNumber);
   ```
4. **Call API endpoint**: `GET /ui/leagues/{id}/scores`
5. **Verify**:
   - ? Weekly winners are marked
   - ? Drop weeks are identified
   - ? Rankings are correct
   - ? Ties are handled properly
   - ? Missing weeks count as drop weeks

---

## ?? Key Features

### Drop Week Logic
- Considers ALL weeks (including weeks with no picks)
- Missed weeks = 0 points
- Takes N lowest scoring weeks per user
- Ties broken by week number (lower week wins)

### Weekly Winner Logic
- Multiple users can win if tied for first
- Ranking shows actual placement (ties = same rank)
- Rank 1 + IsWeeklyWinner = true

### Performance
- Calculations done once per week (batch)
- API reads pre-calculated data
- No complex joins or aggregations at query time

---

## ?? Next Steps

1. Register `LeagueWeekScoringService` in DI
2. Apply database migration
3. Integrate with existing Hangfire weekly scoring job
4. Test with current season data
5. Monitor logs for any issues

---

## ?? Notes

- Service uses `Guid.Empty` as `CreatedBy`/`ModifiedBy` for system operations
- All calculations use `DateTime.UtcNow` for timestamps
- Logging at INFO level for major operations, DEBUG for details
- Handles edge cases:
  - Leagues with no members
  - Weeks with no matchups
  - Users with no picks
  - Leagues with no drop weeks configured

