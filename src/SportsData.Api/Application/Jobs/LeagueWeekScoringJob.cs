using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common.Jobs;

namespace SportsData.Api.Application.Jobs
{
    /// <summary>
    /// Recurring job that ensures all league weeks with finalized contests are scored.
    /// Automatically detects and fills gaps in league week results.
    /// </summary>
    public class LeagueWeekScoringJob : IAmARecurringJob
    {
        private readonly ILogger<LeagueWeekScoringJob> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideCanonicalData _canonicalData;
        private readonly ILeagueWeekScoringService _leagueWeekScoringService;

        public LeagueWeekScoringJob(
            ILogger<LeagueWeekScoringJob> logger,
            AppDataContext dataContext,
            IProvideCanonicalData canonicalData,
            ILeagueWeekScoringService leagueWeekScoringService)
        {
            _logger = logger;
            _dataContext = dataContext;
            _canonicalData = canonicalData;
            _leagueWeekScoringService = leagueWeekScoringService;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Starting {JobName}", nameof(LeagueWeekScoringJob));

            try
            {
                // Get current and previous season weeks
                var seasonWeeks = await _canonicalData.GetCurrentAndLastWeekSeasonWeeks();

                foreach (var seasonWeek in seasonWeeks)
                {
                    _logger.LogInformation(
                        "Processing season year={Year}, week={Week}",
                        seasonWeek.SeasonYear,
                        seasonWeek.WeekNumber);

                    // Get all leagues that have matchups for this week
                    var leagueWeeks = await _dataContext.PickemGroupMatchups
                        .Where(m => m.SeasonYear == seasonWeek.SeasonYear && m.SeasonWeek == seasonWeek.WeekNumber)
                        .Select(m => new { m.GroupId, m.SeasonYear, m.SeasonWeek })
                        .Distinct()
                        .ToListAsync();

                    _logger.LogInformation(
                        "Found {Count} leagues with matchups for week {Week}",
                        leagueWeeks.Count,
                        seasonWeek.WeekNumber);

                    foreach (var leagueWeek in leagueWeeks)
                    {
                        try
                        {
                            // Check if this league week has been scored recently
                            var lastCalculated = await _dataContext.PickemGroupWeekResults
                                .Where(r => 
                                    r.PickemGroupId == leagueWeek.GroupId && 
                                    r.SeasonYear == leagueWeek.SeasonYear && 
                                    r.SeasonWeek == leagueWeek.SeasonWeek)
                                .MaxAsync(r => (DateTime?)r.CalculatedUtc);

                            // Check if all contests for this week are finalized
                            var allContestIds = await _dataContext.PickemGroupMatchups
                                .Where(m => 
                                    m.GroupId == leagueWeek.GroupId && 
                                    m.SeasonYear == leagueWeek.SeasonYear && 
                                    m.SeasonWeek == leagueWeek.SeasonWeek)
                                .Select(m => m.ContestId)
                                .ToListAsync();

                            var finalizedContestIds = await _canonicalData
                                .GetFinalizedContestIds(seasonWeek.Id);

                            var allFinalized = allContestIds.All(id => finalizedContestIds.Contains(id));

                            // Score if:
                            // 1. Never scored (lastCalculated == null), OR
                            // 2. All contests are finalized and it's been more than 1 hour since last calculation
                            var shouldScore = lastCalculated == null || 
                                (allFinalized && lastCalculated < DateTime.UtcNow.AddHours(-1));

                            if (shouldScore)
                            {
                                _logger.LogInformation(
                                    "Scoring league week: leagueId={LeagueId}, year={Year}, week={Week}, reason={Reason}",
                                    leagueWeek.GroupId,
                                    leagueWeek.SeasonYear,
                                    leagueWeek.SeasonWeek,
                                    lastCalculated == null ? "never scored" : "contests finalized");

                                await _leagueWeekScoringService.ScoreLeagueWeekAsync(
                                    leagueWeek.GroupId,
                                    leagueWeek.SeasonYear,
                                    leagueWeek.SeasonWeek);
                            }
                            else
                            {
                                _logger.LogDebug(
                                    "Skipping league week (already scored): leagueId={LeagueId}, year={Year}, week={Week}, lastCalculated={LastCalculated}",
                                    leagueWeek.GroupId,
                                    leagueWeek.SeasonYear,
                                    leagueWeek.SeasonWeek,
                                    lastCalculated);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Error scoring league week: leagueId={LeagueId}, year={Year}, week={Week}",
                                leagueWeek.GroupId,
                                leagueWeek.SeasonYear,
                                leagueWeek.SeasonWeek);
                            // Continue processing other leagues
                        }
                    }
                }

                _logger.LogInformation("Completed {JobName}", nameof(LeagueWeekScoringJob));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in {JobName}", nameof(LeagueWeekScoringJob));
                throw;
            }
        }
    }
}
