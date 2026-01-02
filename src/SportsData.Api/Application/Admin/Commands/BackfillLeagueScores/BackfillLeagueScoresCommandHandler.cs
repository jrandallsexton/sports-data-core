using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;

namespace SportsData.Api.Application.Admin.Commands.BackfillLeagueScores;

public interface IBackfillLeagueScoresCommandHandler
{
    Task<BackfillLeagueScoresResult> ExecuteAsync(BackfillLeagueScoresCommand command, CancellationToken cancellationToken);
}

public class BackfillLeagueScoresCommandHandler : IBackfillLeagueScoresCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IProvideCanonicalData _canonicalData;
    private readonly ILeagueWeekScoringService _leagueWeekScoringService;
    private readonly IValidator<BackfillLeagueScoresCommand> _validator;
    private readonly ILogger<BackfillLeagueScoresCommandHandler> _logger;

    public BackfillLeagueScoresCommandHandler(
        AppDataContext dataContext,
        IProvideCanonicalData canonicalData,
        ILeagueWeekScoringService leagueWeekScoringService,
        IValidator<BackfillLeagueScoresCommand> validator,
        ILogger<BackfillLeagueScoresCommandHandler> logger)
    {
        _dataContext = dataContext;
        _canonicalData = canonicalData;
        _leagueWeekScoringService = leagueWeekScoringService;
        _validator = validator;
        _logger = logger;
    }

    public async Task<BackfillLeagueScoresResult> ExecuteAsync(
        BackfillLeagueScoresCommand command,
        CancellationToken cancellationToken)
    {
        // Validate command
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return new BackfillLeagueScoresResult(
                command.SeasonYear,
                0,
                0,
                1,
                $"Validation failed: {errors}"
            );
        }

        _logger.LogInformation(
            "Starting league scores backfill for season year {SeasonYear}",
            command.SeasonYear);

        try
        {
            // Get all completed weeks for the season
            var seasonWeeks = await _canonicalData.GetCompletedSeasonWeeks(command.SeasonYear);

            if (seasonWeeks.Count == 0)
            {
                _logger.LogWarning("No completed weeks found for season year {SeasonYear}", command.SeasonYear);
                return new BackfillLeagueScoresResult(
                    command.SeasonYear,
                    0,
                    0,
                    0,
                    "No completed weeks found for this season"
                );
            }

            _logger.LogInformation(
                "Found {WeekCount} completed weeks for season {SeasonYear}",
                seasonWeeks.Count,
                command.SeasonYear);

            var processedLeagueWeeks = 0;
            var errors = 0;

            // Process each week
            foreach (var seasonWeek in seasonWeeks)
            {
                _logger.LogInformation(
                    "Processing season year={Year}, week={Week}",
                    seasonWeek.SeasonYear,
                    seasonWeek.WeekNumber);

                try
                {
                    // Get all DISTINCT league/year/week combinations
                    var leagueWeeks = await _dataContext.PickemGroupMatchups
                        .Where(m => m.SeasonYear == seasonWeek.SeasonYear && m.SeasonWeek == seasonWeek.WeekNumber)
                        .Select(m => new { m.GroupId, m.SeasonYear, m.SeasonWeek })
                        .Distinct()
                        .ToListAsync(cancellationToken);

                    _logger.LogInformation(
                        "Found {Count} leagues with matchups for week {Week}",
                        leagueWeeks.Count,
                        seasonWeek.WeekNumber);

                    // Score each league/week combination
                    foreach (var leagueWeek in leagueWeeks)
                    {
                        try
                        {
                            await _leagueWeekScoringService.ScoreLeagueWeekAsync(
                                leagueWeek.GroupId,
                                leagueWeek.SeasonYear,
                                leagueWeek.SeasonWeek);

                            processedLeagueWeeks++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Failed to process league week: leagueId={LeagueId}, year={Year}, week={Week}",
                                leagueWeek.GroupId,
                                leagueWeek.SeasonYear,
                                leagueWeek.SeasonWeek);
                            errors++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to process season week: year={Year}, week={Week}",
                        seasonWeek.SeasonYear,
                        seasonWeek.WeekNumber);
                    errors++;
                }
            }

            var message = $"Backfill completed: processed {processedLeagueWeeks} league weeks with {errors} errors";
            _logger.LogInformation(message);

            return new BackfillLeagueScoresResult(
                command.SeasonYear,
                seasonWeeks.Count,
                processedLeagueWeeks,
                errors,
                message
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backfill failed for season {SeasonYear}", command.SeasonYear);
            return new BackfillLeagueScoresResult(
                command.SeasonYear,
                0,
                0,
                1,
                $"Backfill failed: {ex.Message}"
            );
        }
    }
}
