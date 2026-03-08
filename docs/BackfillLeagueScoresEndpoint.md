# Backfill League Scores Endpoint

## Purpose
Backfills league week scores for all completed weeks in a season. Useful for:
- Initial data population
- Fixing historical data issues
- Reprocessing after scoring logic changes

## Endpoint
```
POST /admin/backfill-league-scores/{seasonYear}
```

## Parameters
- `seasonYear` (int) - The season year to backfill (e.g., 2024, 2025)

## Authentication
Requires admin API token (via `AdminApiToken` attribute)

## How It Works
1. Retrieves all completed season weeks for the specified year
2. For each league/week combination, calls `ScoreLeagueWeekAsync()` to:
   - Calculate scores for the league's matchups that week
   - Determine weekly winners (with tiebreaker resolution)
   - Mark drop weeks
   - Assign rankings

## Response

The response is keyed by league and week, reflecting the per-league/week processing model of `ScoreLeagueWeekAsync()`:

```json
{
  "seasonYear": 2025,
  "totalWeeks": 15,
  "results": [
    {
      "leagueId": "abc123",
      "week": 1,
      "matchupsScored": 6,
      "status": "success"
    },
    {
      "leagueId": "abc123",
      "week": 2,
      "matchupsScored": 6,
      "status": "success"
    },
    {
      "leagueId": "def456",
      "week": 1,
      "matchupsScored": 4,
      "status": "success"
    }
  ],
  "summary": {
    "totalLeagueWeeks": 45,
    "successful": 45,
    "errors": 0
  }
}
```

## Example Usage

### Using curl
```bash
curl -X POST https://your-api.com/admin/backfill-league-scores/2025 \
  -H "X-Admin-Token: YOUR_ADMIN_TOKEN"
```

### Using Postman
1. Method: POST
2. URL: `https://your-api.com/admin/backfill-league-scores/2025`
3. Headers:
   - `X-Admin-Token: YOUR_ADMIN_TOKEN`

## Process Flow
```
GET completed weeks for season ? FOR EACH week:
  ?? Get all leagues with matchups
  ?? FOR EACH league:
  ?   ?? Calculate user scores
  ?   ?? Determine winners (with tiebreaker)
  ?   ?? Assign rankings
  ?   ?? Mark drop weeks
  ?? Save results
```

## Logging
The endpoint logs:
- Start of backfill operation
- Number of weeks found
- Processing of each individual week
- Completion with summary stats
- Any errors encountered

Check application logs for detailed progress.

## Notes
- **Idempotent**: Safe to run multiple times - will update existing records
- **Async**: Runs synchronously but could be moved to background job if needed
- **Error Handling**: Continues processing even if individual weeks fail
- **Performance**: May take several seconds for a full season

## Related Code
- `LeagueWeekScoringService.ScoreLeagueWeekAsync()`
- `LeagueWeekScoringJob` (runs weekly as safety net)
- `ContestScoringProcessor` (triggers real-time scoring)
