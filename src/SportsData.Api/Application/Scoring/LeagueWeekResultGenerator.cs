using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.Scoring
{
    public interface ILeagueWeekResultGenerator
    {
        Task GenerateAsync(CancellationToken cancellationToken = default);
    }

    public class LeagueWeekResultGenerator : ILeagueWeekResultGenerator
    {
        private readonly AppDataContext _db;
        private readonly ILogger<LeagueWeekResultGenerator> _logger;

        public LeagueWeekResultGenerator(AppDataContext db, ILogger<LeagueWeekResultGenerator> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task GenerateAsync(CancellationToken cancellationToken = default)
        {
            var picks = await _db.UserPicks
                .Include(p => p.Contest)
                .Where(p =>
                    p.IsCorrect != null &&
                    p.ScoredAt != null &&
                    p.Contest.FinalizedUtc != null)
                .ToListAsync(cancellationToken);

            if (picks.Count == 0)
            {
                _logger.LogInformation("No scored picks found for rollup.");
                return;
            }

            var leagueIds = picks.Select(p => p.PickemGroupId).Distinct().ToList();
            var leagueMap = await _db.PickemGroups
                .Where(l => leagueIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, cancellationToken);

            var rollups = picks
                .GroupBy(p => new
                {
                    PickemGroupId = p.PickemGroupId,
                    p.UserId,
                    p.Contest.SeasonYear,
                    p.Contest.SeasonWeek
                })
                .Select(g => new PickemGroupWeekResult
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = g.Key.PickemGroupId,
                    UserId = g.Key.UserId,
                    SeasonYear = g.Key.SeasonYear,
                    SeasonWeek = g.Key.SeasonWeek,
                    TotalPoints = g.Sum(p => p.PointsAwarded ?? 0),
                    CorrectPicks = g.Count(p => p.IsCorrect == true),
                    TotalPicks = g.Count(),
                    IsWeeklyWinner = false,
                    CalculatedUtc = DateTime.UtcNow
                })
                .ToList();

            if (rollups.Count == 0)
            {
                _logger.LogInformation("No rollup results to insert.");
                return;
            }

            var groupedByWeek = rollups
                .GroupBy(r => new { PickemGroupId = r.PickemGroupId, r.SeasonYear, r.SeasonWeek });

            foreach (var group in groupedByWeek)
            {
                var topScore = group.Max(r => r.TotalPoints);
                var topResults = group.Where(r => r.TotalPoints == topScore).ToList();

                if (topResults.Count == 1)
                {
                    topResults[0].IsWeeklyWinner = true;
                    continue;
                }

                var league = leagueMap[group.Key.PickemGroupId];

                if (league.TiebreakerType != TiebreakerType.None)
                {
                    var tieCandidates = topResults
                        .Select(result =>
                        {
                            var userPicks = picks.Where(p =>
                                p.PickemGroupId == result.PickemGroupId &&
                                p.UserId == result.UserId &&
                                p.Contest.SeasonYear == result.SeasonYear &&
                                p.Contest.SeasonWeek == result.SeasonWeek).ToList();

                            int error = league.TiebreakerType switch
                            {
                                TiebreakerType.TotalPoints => userPicks
                                    .Where(p => p.TiebreakerGuessTotal.HasValue && p.TiebreakerActualTotal.HasValue)
                                    .Sum(p => Math.Abs(p.TiebreakerGuessTotal!.Value - p.TiebreakerActualTotal!.Value)),

                                TiebreakerType.HomeAndAwayScores => userPicks
                                    .Where(p =>
                                        p.TiebreakerGuessHome.HasValue &&
                                        p.TiebreakerGuessAway.HasValue &&
                                        p.TiebreakerActualHome.HasValue &&
                                        p.TiebreakerActualAway.HasValue)
                                    .Sum(p =>
                                        Math.Abs(p.TiebreakerGuessHome!.Value - p.TiebreakerActualHome!.Value) +
                                        Math.Abs(p.TiebreakerGuessAway!.Value - p.TiebreakerActualAway!.Value)),

                                _ => int.MaxValue
                            };

                            var firstSubmitted = userPicks.Min(p => p.ScoredAt) ?? DateTime.MaxValue;
                            return (Result: result, TiebreakerError: error, FirstSubmission: firstSubmitted);
                        })
                        .ToList();

                    var lowestError = tieCandidates.Min(tc => tc.TiebreakerError);
                    var finalists = tieCandidates.Where(tc => tc.TiebreakerError == lowestError).ToList();

                    if (finalists.Count == 1)
                    {
                        finalists[0].Result.IsWeeklyWinner = true;
                    }
                    else
                    {
                        switch (league.TiebreakerTiePolicy)
                        {
                            case TiebreakerTiePolicy.EarliestSubmission:
                                var earliest = finalists.Min(tc => tc.FirstSubmission);
                                foreach (var winner in finalists.Where(tc => tc.FirstSubmission == earliest))
                                    winner.Result.IsWeeklyWinner = true;
                                break;

                            // Uncomment when adding support for additional policies
                            // case TiebreakerTiePolicy.RandomSelection:
                            // case TiebreakerTiePolicy.CommissionerDecision:

                            default:
                                foreach (var winner in finalists)
                                    winner.Result.IsWeeklyWinner = true;
                                break;
                        }
                    }
                }
                else
                {
                    foreach (var result in topResults)
                        result.IsWeeklyWinner = true;
                }
            }

            var keySet = rollups
                .Select(r => new { PickemGroupId = r.PickemGroupId, r.UserId, r.SeasonYear, r.SeasonWeek })
                .ToHashSet();

            var existing = _db.PickemGroupWeekResults
                .AsEnumerable()
                .Where(r => keySet.Contains(new
                {
                    PickemGroupId = r.PickemGroupId,
                    r.UserId,
                    r.SeasonYear,
                    r.SeasonWeek
                }))
                .ToList();

            _db.PickemGroupWeekResults.RemoveRange(existing);
            await _db.PickemGroupWeekResults.AddRangeAsync(rollups, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Generated {Count} weekly league results.", rollups.Count);
        }
    }
}
