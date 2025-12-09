# ?? Final Implementation Steps

## ? What's Already Complete

1. ? **Entity Updated** - `PickemGroupWeekResult` now has `IsDropWeek` and `Rank`
2. ? **Service Created** - `LeagueWeekScoringService` fully implemented
3. ? **Interface Created** - `ILeagueWeekScoringService` defined
4. ? **DTO Updated** - `LeagueScoresByWeekDto` has `IsWeeklyWinner` and `Rank`
5. ? **Endpoint Updated** - `LeagueService.GetLeagueScoresByWeek()` reads from DB
6. ? **DbSet Exists** - `AppDataContext.PickemGroupWeekResults` already configured
7. ? **Build Passing** - All code compiles successfully

---

## ?? Remaining Steps (In Order)

### 1. Apply Database Migration

Since EF tools aren't installed globally, you can either:

**Option A: Install EF Tools (Recommended)**
```powershell
dotnet tool install --global dotnet-ef
```

Then create and apply migration:
```powershell
cd src\SportsData.Api
dotnet ef migrations add AddDropWeekAndRankToPickemGroupWeekResult --context AppDataContext
dotnet ef database update --context AppDataContext
```

**Option B: Run SQL Manually**
```sql
-- From: docs/migrations/AddDropWeekAndRankToPickemGroupWeekResult.sql

ALTER TABLE "PickemGroupWeekResult" 
ADD "IsDropWeek" boolean NOT NULL DEFAULT false;

ALTER TABLE "PickemGroupWeekResult" 
ADD "Rank" integer NULL;

-- Optional index for performance:
CREATE INDEX "IX_PickemGroupWeekResult_IsDropWeek" 
ON "PickemGroupWeekResult" ("PickemGroupId", "SeasonYear", "IsDropWeek") 
INCLUDE ("UserId", "SeasonWeek", "TotalPoints");
```

Note: The table name in your database might be `"LeagueWeekResult"` based on the `ToTable()` call in the configuration.

### 2. Register Service in DI

Find your service registration (likely in `Program.cs` or `Startup.cs` in `SportsData.Api` project) and add:

```csharp
// Add this with your other scoped services
services.AddScoped<ILeagueWeekScoringService, LeagueWeekScoringService>();
```

### 3. Find and Update Your Hangfire Job

Look for where picks are scored (likely a Hangfire job or background service). Update it to call league scoring after pick scoring:

```csharp
// Example - your actual implementation may vary
public class WeeklyScoringJob
{
    private readonly IPickScoringService _pickScoringService;
    private readonly ILeagueWeekScoringService _leagueScoringService;
    private readonly ILogger<WeeklyScoringJob> _logger;
    
    public WeeklyScoringJob(
        IPickScoringService pickScoringService,
        ILeagueWeekScoringService leagueScoringService,
        ILogger<WeeklyScoringJob> logger)
    {
        _pickScoringService = pickScoringService;
        _leagueScoringService = leagueScoringService;
        _logger = logger;
    }
    
    public async Task ScoreWeek(int seasonYear, int weekNumber)
    {
        _logger.LogInformation("Starting weekly scoring for {Year} Week {Week}", seasonYear, weekNumber);
        
        // 1. Score individual picks first (EXISTING)
        await _pickScoringService.ScorePicksForWeekAsync(seasonYear, weekNumber);
        
        // 2. Score leagues for the week (NEW)
        await _leagueScoringService.ScoreAllLeaguesForWeekAsync(seasonYear, weekNumber);
        
        _logger.LogInformation("Completed weekly scoring for {Year} Week {Week}", seasonYear, weekNumber);
    }
}
```

### 4. Backfill Current Season Data

Create a one-time job or console app to populate historical data:

```csharp
public class BackfillLeagueScores
{
    private readonly ILeagueWeekScoringService _scoringService;
    private readonly AppDataContext _dbContext;
    
    public async Task BackfillSeason2024()
    {
        var allWeeks = await _dbContext.PickemGroupMatchups
            .Where(m => m.SeasonYear == 2024)
            .Select(m => m.SeasonWeek)
            .Distinct()
            .OrderBy(w => w)
            .ToListAsync();
        
        foreach (var week in allWeeks)
        {
            await _scoringService.ScoreAllLeaguesForWeekAsync(2024, week);
        }
    }
}
```

Or run it manually from a controller endpoint (temporarily):
```csharp
[HttpPost("admin/backfill-league-scores")]
[Authorize] // Make sure to protect this!
public async Task<IActionResult> BackfillLeagueScores()
{
    var scoringService = HttpContext.RequestServices.GetRequiredService<ILeagueWeekScoringService>();
    
    // Backfill all weeks for 2024
    for (int week = 1; week <= 15; week++) // Adjust based on your season
    {
        await scoringService.ScoreAllLeaguesForWeekAsync(2024, week);
    }
    
    return Ok("Backfill complete");
}
```

### 5. Verify Everything Works

1. **Check migration applied**:
   ```sql
   SELECT column_name, data_type 
   FROM information_schema.columns 
   WHERE table_name = 'LeagueWeekResult';
   -- Should show IsDropWeek and Rank columns
   ```

2. **Run backfill** (if needed)

3. **Test the endpoint**:
   ```
   GET /ui/leagues/{your-league-id}/scores
   ```

4. **Verify the response** contains:
   - `isWeeklyWinner` = true for first place (including ties)
   - `isDropWeek` = true for N lowest weeks per user
   - `rank` = proper rankings for each week
   - Missing weeks count as drop weeks

---

## ?? Testing Checklist

- [ ] Migration applied successfully
- [ ] Service registered in DI
- [ ] Hangfire job updated to call league scoring
- [ ] Backfill ran successfully
- [ ] API endpoint returns data with new fields
- [ ] Weekly winners correctly identified (including ties)
- [ ] Drop weeks correctly marked (N lowest per user)
- [ ] Missing weeks count as drop weeks
- [ ] Rankings are correct

---

## ?? Notes

### Table Name
Check your actual table name in the database. The entity configuration shows:
```csharp
builder.ToTable("LeagueWeekResult");
```
So you might need to use `"LeagueWeekResult"` instead of `"PickemGroupWeekResult"` in SQL scripts.

### PostgreSQL vs SQL Server
The migration script provided uses PostgreSQL syntax. If you're using SQL Server, adjust the syntax accordingly.

### Logging
The `LeagueWeekScoringService` logs at INFO level for major operations. Monitor these logs to ensure everything runs correctly.

### Performance
The service processes leagues individually in a loop. If you have many leagues, consider:
- Running as a background job
- Adding progress tracking
- Implementing batching if needed

---

## ?? Once Complete

After all steps are done:
1. Your API will return pre-calculated weekly scores
2. Weekly winners will be automatically determined (with tie handling)
3. Drop weeks will be automatically calculated
4. Rankings will be available for each week
5. Performance will be excellent (no calculations on-demand)

Let me know if you run into any issues!
