using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.Scoring;

/// <summary>
/// Service responsible for calculating weekly league scores, determining winners,
/// and identifying drop weeks for each user in a league.
/// </summary>
public class LeagueWeekScoringService : ILeagueWeekScoringService
{
    private readonly AppDataContext _dbContext;
    private readonly ILogger<LeagueWeekScoringService> _logger;

    public LeagueWeekScoringService(
        AppDataContext dbContext,
        ILogger<LeagueWeekScoringService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Scores a specific week for a specific league.
    /// Calculates user scores, determines weekly winners, and marks drop weeks.
    /// </summary>
    public async Task ScoreLeagueWeekAsync(Guid leagueId, int seasonYear, int weekNumber, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting league week scoring for leagueId={LeagueId}, seasonYear={SeasonYear}, week={Week}",
            leagueId,
            seasonYear,
            weekNumber);

        var league = await _dbContext.PickemGroups
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == leagueId, cancellationToken);

        if (league == null)
        {
            _logger.LogWarning(
                "League not found for scoring, leagueId={LeagueId}",
                leagueId);
            return;
        }

        // Get all matchups for this league and week
        var weekMatchups = await _dbContext.PickemGroupMatchups
            .Where(m => m.GroupId == leagueId && m.SeasonWeek == weekNumber)
            .Select(m => m.ContestId)
            .ToListAsync(cancellationToken);

        if (weekMatchups.Count == 0)
        {
            _logger.LogInformation(
                "No matchups found for leagueId={LeagueId}, week={Week}",
                leagueId,
                weekNumber);
            return;
        }

        // Get all user picks for this week
        var userPicks = await _dbContext.UserPicks
            .Where(p => p.PickemGroupId == leagueId && weekMatchups.Contains(p.ContestId))
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Found {PickCount} picks for {UserCount} users in leagueId={LeagueId}, week={Week}",
            userPicks.Count,
            userPicks.Select(p => p.UserId).Distinct().Count(),
            leagueId,
            weekNumber);

        // Calculate scores per user for this week
        var userScores = userPicks
            .GroupBy(p => p.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPoints = g.Where(p => p.PointsAwarded.HasValue).Sum(p => p.PointsAwarded!.Value),
                CorrectPicks = g.Count(p => p.PointsAwarded.HasValue && p.PointsAwarded.Value > 0),
                TotalPicks = g.Count()
            })
            .ToList();

        // Add users with no picks (score of 0)
        var usersWithPicks = userScores.Select(s => s.UserId).ToHashSet();
        var usersWithoutPicks = league.Members
            .Where(m => !usersWithPicks.Contains(m.UserId))
            .Select(m => new
            {
                UserId = m.UserId,
                TotalPoints = 0,
                CorrectPicks = 0,
                TotalPicks = 0
            })
            .ToList();

        var allUserScores = userScores.Concat(usersWithoutPicks).ToList();

        _logger.LogInformation(
            "Calculated scores for {UserCount} users in leagueId={LeagueId}, week={Week}",
            allUserScores.Count,
            leagueId,
            weekNumber);

        // Create or update PickemGroupWeekResult records
        foreach (var userScore in allUserScores)
        {
            var result = await _dbContext.PickemGroupWeekResults
                .FirstOrDefaultAsync(r =>
                    r.PickemGroupId == leagueId &&
                    r.UserId == userScore.UserId &&
                    r.SeasonYear == seasonYear &&
                    r.SeasonWeek == weekNumber,
                    cancellationToken);

            if (result == null)
            {
                result = new PickemGroupWeekResult
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userScore.UserId,
                    SeasonYear = seasonYear,
                    SeasonWeek = weekNumber,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.Empty // System
                };
                _dbContext.PickemGroupWeekResults.Add(result);
            }

            result.TotalPoints = userScore.TotalPoints;
            result.CorrectPicks = userScore.CorrectPicks;
            result.TotalPicks = userScore.TotalPicks;
            result.CalculatedUtc = DateTime.UtcNow;
            result.ModifiedUtc = DateTime.UtcNow;
            result.ModifiedBy = Guid.Empty; // System
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Determine weekly winners and rankings
        await DetermineWeeklyWinnersAsync(leagueId, seasonYear, weekNumber, cancellationToken);

        // Calculate and mark drop weeks
        await CalculateDropWeeksAsync(leagueId, seasonYear, league.DropLowWeeksCount ?? 0, cancellationToken);

        _logger.LogInformation(
            "Completed league week scoring for leagueId={LeagueId}, seasonYear={SeasonYear}, week={Week}",
            leagueId,
            seasonYear,
            weekNumber);
    }

    /// <summary>
    /// Determines weekly winners and assigns rankings for a specific week.
    /// Uses league's tiebreaker type to resolve ties.
    /// </summary>
    private async Task DetermineWeeklyWinnersAsync(
        Guid leagueId,
        int seasonYear,
        int weekNumber,
        CancellationToken cancellationToken)
    {
        // Get league to check tiebreaker type
        var league = await _dbContext.PickemGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == leagueId, cancellationToken);

        if (league == null)
        {
            _logger.LogWarning(
                "League not found for winner determination, leagueId={LeagueId}",
                leagueId);
            return;
        }

        var weekResults = await _dbContext.PickemGroupWeekResults
            .Where(r => r.PickemGroupId == leagueId &&
                       r.SeasonYear == seasonYear &&
                       r.SeasonWeek == weekNumber)
            .ToListAsync(cancellationToken);

        if (weekResults.Count == 0)
        {
            _logger.LogWarning(
                "No week results found for winner determination, leagueId={LeagueId}, week={Week}",
                leagueId,
                weekNumber);
            return;
        }

        var highScore = weekResults.Max(r => r.TotalPoints);
        var tiedUsers = weekResults.Where(r => r.TotalPoints == highScore).ToList();

        // If there's only one user with high score, they're the winner
        if (tiedUsers.Count == 1)
        {
            foreach (var result in weekResults)
            {
                result.IsWeeklyWinner = result.TotalPoints == highScore;
                result.Rank = weekResults.Count(r => r.TotalPoints > result.TotalPoints) + 1;
            }
        }
        // Handle ties based on league's tiebreaker type
        else if (league.TiebreakerType == TiebreakerType.EarliestSubmission)
        {
            // Get earliest pick submission time for each tied user
            var weekMatchups = await _dbContext.PickemGroupMatchups
                .Where(m => m.GroupId == leagueId && m.SeasonWeek == weekNumber)
                .Select(m => m.ContestId)
                .ToListAsync(cancellationToken);

            var userPickTimes = new Dictionary<Guid, DateTime>();

            foreach (var user in tiedUsers)
            {
                var earliestPick = await _dbContext.UserPicks
                    .Where(p => p.PickemGroupId == leagueId && 
                               p.UserId == user.UserId && 
                               weekMatchups.Contains(p.ContestId))
                    .OrderBy(p => p.CreatedUtc)
                    .Select(p => p.CreatedUtc)
                    .FirstOrDefaultAsync(cancellationToken);

                userPickTimes[user.UserId] = earliestPick;
            }

            // User with earliest submission wins
            var earliestTime = userPickTimes.Values.Min();
            var winnerUserId = userPickTimes.First(kvp => kvp.Value == earliestTime).Key;

            _logger.LogInformation(
                "Tiebreaker resolved by earliest submission for leagueId={LeagueId}, week={Week}, winner={WinnerUserId}, time={EarliestTime}",
                leagueId,
                weekNumber,
                winnerUserId,
                earliestTime);

            foreach (var result in weekResults)
            {
                result.IsWeeklyWinner = result.UserId == winnerUserId;
                result.Rank = weekResults.Count(r => r.TotalPoints > result.TotalPoints) + 1;
            }
        }
        else
        {
            // For None, TotalPoints, or HomeAndAwayScores - allow multiple winners
            // (Those tiebreaker types are for individual picks, not league-wide)
            _logger.LogInformation(
                "Multiple winners due to tie for leagueId={LeagueId}, week={Week}, tiebreakerType={TiebreakerType}",
                leagueId,
                weekNumber,
                league.TiebreakerType);

            foreach (var result in weekResults)
            {
                result.IsWeeklyWinner = result.TotalPoints == highScore;
                result.Rank = weekResults.Count(r => r.TotalPoints > result.TotalPoints) + 1;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var winnerCount = weekResults.Count(r => r.IsWeeklyWinner);
        _logger.LogInformation(
            "Determined {WinnerCount} weekly winner(s) with score {HighScore} for leagueId={LeagueId}, week={Week}",
            winnerCount,
            highScore,
            leagueId,
            weekNumber);
    }

    /// <summary>
    /// Calculates and marks drop weeks for all users in a league.
    /// Drop weeks are the N lowest-scoring weeks for each user (including missed weeks as 0).
    /// </summary>
    private async Task<HashSet<(Guid UserId, int WeekNumber)>> CalculateDropWeeksAsync(
        Guid leagueId,
        int seasonYear,
        int dropCount,
        CancellationToken cancellationToken)
    {
        var dropWeeks = new HashSet<(Guid, int)>();

        if (dropCount <= 0)
        {
            _logger.LogDebug(
                "No drop weeks configured for leagueId={LeagueId}",
                leagueId);
            return dropWeeks;
        }

        // Get all week results for this league
        var allResults = await _dbContext.PickemGroupWeekResults
            .Where(r => r.PickemGroupId == leagueId && r.SeasonYear == seasonYear)
            .ToListAsync(cancellationToken);

        // Get all weeks that have matchups
        var allWeeks = await _dbContext.PickemGroupMatchups
            .Where(m => m.GroupId == leagueId)
            .Select(m => m.SeasonWeek)
            .Distinct()
            .OrderBy(w => w)
            .ToListAsync(cancellationToken);

        // Get all users in the league
        var allUsers = await _dbContext.PickemGroupMembers
            .Where(m => m.PickemGroupId == leagueId)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Calculating drop weeks for {UserCount} users across {WeekCount} weeks, dropCount={DropCount}, leagueId={LeagueId}",
            allUsers.Count,
            allWeeks.Count,
            dropCount,
            leagueId);

        // Reset all IsDropWeek flags first
        foreach (var result in allResults)
        {
            result.IsDropWeek = false;
        }

        // For each user, find their N lowest scoring weeks
        foreach (var userId in allUsers)
        {
            var userResults = allResults
                .Where(r => r.UserId == userId)
                .Select(r => (WeekNumber: r.SeasonWeek, Score: r.TotalPoints))
                .ToList();

            // Add missing weeks as 0 scores
            var missedWeeks = allWeeks
                .Except(userResults.Select(r => r.WeekNumber))
                .Select(w => (WeekNumber: w, Score: 0))
                .ToList();

            var allUserScores = userResults.Concat(missedWeeks).ToList();

            // Get the N lowest scoring weeks
            var lowestScores = allUserScores
                .OrderBy(s => s.Score)
                .ThenBy(s => s.WeekNumber) // Consistent tiebreaker
                .Take(dropCount)
                .Select(s => s.WeekNumber)
                .ToList();

            // Mark these weeks as drop weeks in the results
            foreach (var weekNumber in lowestScores)
            {
                var result = allResults.FirstOrDefault(r =>
                    r.UserId == userId &&
                    r.SeasonWeek == weekNumber);

                if (result != null)
                {
                    result.IsDropWeek = true;
                    dropWeeks.Add((userId, weekNumber));
                }
                else
                {
                    // This is a missed week - it counts as a drop week but has no record
                    dropWeeks.Add((userId, weekNumber));
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Marked {DropWeekCount} drop weeks for leagueId={LeagueId}",
            dropWeeks.Count,
            leagueId);

        return dropWeeks;
    }

    /// <summary>
    /// Scores all active leagues for a specific week.
    /// Called by Hangfire job after picks have been scored.
    /// </summary>
    public async Task ScoreAllLeaguesForWeekAsync(int seasonYear, int weekNumber, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting scoring for all leagues, seasonYear={SeasonYear}, week={Week}",
            seasonYear,
            weekNumber);

        // Get all leagues that have matchups for this week AND season year
        var leaguesWithMatchups = await _dbContext.PickemGroupMatchups
            .Where(m => m.SeasonYear == seasonYear && m.SeasonWeek == weekNumber)
            .Select(m => m.GroupId)
            .Distinct()
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Found {LeagueCount} leagues with matchups for seasonYear={SeasonYear}, week={Week}",
            leaguesWithMatchups.Count,
            seasonYear,
            weekNumber);

        var successCount = 0;
        var failureCount = 0;

        foreach (var leagueId in leaguesWithMatchups)
        {
            try
            {
                await ScoreLeagueWeekAsync(leagueId, seasonYear, weekNumber, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to score league week for leagueId={LeagueId}, seasonYear={SeasonYear}, week={Week}",
                    leagueId,
                    seasonYear,
                    weekNumber);
                failureCount++;
            }
        }

        _logger.LogInformation(
            "Completed scoring all leagues for seasonYear={SeasonYear}, week={Week}: {SuccessCount} successful, {FailureCount} failed",
            seasonYear,
            weekNumber,
            successCount,
            failureCount);
    }
}
