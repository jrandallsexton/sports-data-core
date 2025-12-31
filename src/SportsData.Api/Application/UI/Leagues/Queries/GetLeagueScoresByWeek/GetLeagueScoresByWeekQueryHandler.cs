using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueScoresByWeek;

public interface IGetLeagueScoresByWeekQueryHandler
{
    Task<Result<LeagueScoresByWeekDto>> ExecuteAsync(
        GetLeagueScoresByWeekQuery query,
        CancellationToken cancellationToken = default);
}

public class GetLeagueScoresByWeekQueryHandler : IGetLeagueScoresByWeekQueryHandler
{
    private readonly ILogger<GetLeagueScoresByWeekQueryHandler> _logger;
    private readonly AppDataContext _dbContext;

    public GetLeagueScoresByWeekQueryHandler(
        ILogger<GetLeagueScoresByWeekQueryHandler> logger,
        AppDataContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<Result<LeagueScoresByWeekDto>> ExecuteAsync(
        GetLeagueScoresByWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        var league = await _dbContext.PickemGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.LeagueId, cancellationToken);

        if (league is null)
            return new Failure<LeagueScoresByWeekDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.LeagueId), $"League with ID {query.LeagueId} not found.")]);

        var weekResults = await _dbContext.PickemGroupWeekResults
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.PickemGroupId == query.LeagueId)
            .OrderBy(r => r.SeasonWeek)
            .ThenByDescending(r => r.TotalPoints)
            .ToListAsync(cancellationToken);

        if (weekResults.Count == 0)
        {
            _logger.LogWarning(
                "No week results found for leagueId={LeagueId}. Results may not have been calculated yet.",
                query.LeagueId);

            return new Success<LeagueScoresByWeekDto>(new LeagueScoresByWeekDto
            {
                LeagueId = query.LeagueId,
                LeagueName = league.Name,
                Weeks = []
            });
        }

        var result = new LeagueScoresByWeekDto
        {
            LeagueId = query.LeagueId,
            LeagueName = league.Name,
            Weeks = weekResults
                .GroupBy(r => r.SeasonWeek)
                .Select(g => new LeagueScoresByWeekDto.LeagueScoreByWeek
                {
                    WeekNumber = g.Key,
                    PickCount = g.First().TotalPicks,
                    UserScores = g.Select(r => new LeagueScoresByWeekDto.LeagueUserScoreDto
                    {
                        UserId = r.UserId,
                        UserName = r.User?.DisplayName ?? "Unknown",
                        IsSynthetic = r.User?.IsSynthetic ?? false,
                        WeekNumber = r.SeasonWeek,
                        PickCount = r.TotalPicks,
                        Score = r.TotalPoints,
                        IsDropWeek = r.IsDropWeek,
                        IsWeeklyWinner = r.IsWeeklyWinner,
                        Rank = r.Rank
                    }).ToList()
                }).ToList()
        };

        _logger.LogInformation(
            "Retrieved scores for {WeekCount} weeks for leagueId={LeagueId}",
            result.Weeks.Count,
            query.LeagueId);

        return new Success<LeagueScoresByWeekDto>(result);
    }
}
