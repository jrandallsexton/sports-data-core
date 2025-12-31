using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekOverview;

public interface IGetLeagueWeekOverviewQueryHandler
{
    Task<Result<LeagueWeekOverviewDto>> ExecuteAsync(
        GetLeagueWeekOverviewQuery query,
        CancellationToken cancellationToken = default);
}

public class GetLeagueWeekOverviewQueryHandler : IGetLeagueWeekOverviewQueryHandler
{
    private readonly ILogger<GetLeagueWeekOverviewQueryHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly IProvideCanonicalData _canonicalDataProvider;
    private readonly IGetUserPicksByGroupAndWeekQueryHandler _userPicksQueryHandler;

    public GetLeagueWeekOverviewQueryHandler(
        ILogger<GetLeagueWeekOverviewQueryHandler> logger,
        AppDataContext dbContext,
        IProvideCanonicalData canonicalDataProvider,
        IGetUserPicksByGroupAndWeekQueryHandler userPicksQueryHandler)
    {
        _logger = logger;
        _dbContext = dbContext;
        _canonicalDataProvider = canonicalDataProvider;
        _userPicksQueryHandler = userPicksQueryHandler;
    }

    public async Task<Result<LeagueWeekOverviewDto>> ExecuteAsync(
        GetLeagueWeekOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var league = await _dbContext.PickemGroups
            .AsNoTracking()
            .Include(x => x.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == query.LeagueId, cancellationToken);

        if (league is null)
            return new Failure<LeagueWeekOverviewDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.LeagueId), $"League with ID {query.LeagueId} not found.")]);

        var matchups = await _dbContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.GroupId == query.LeagueId && m.SeasonWeek == query.Week)
            .ToListAsync(cancellationToken);

        var contestIds = matchups
            .Select(m => m.ContestId)
            .ToList();

        var result = new LeagueWeekOverviewDto();

        var canonicalContests = await _canonicalDataProvider
            .GetContestResultsByContestIds(contestIds);

        foreach (var canonicalContest in canonicalContests)
        {
            var matchup = matchups
                .FirstOrDefault(m => m.ContestId == canonicalContest.ContestId);

            if (matchup is null)
            {
                _logger.LogError("Matchup could not be found for contest {ContestId}", canonicalContest.ContestId);
                return new Failure<LeagueWeekOverviewDto>(
                    default!,
                    ResultStatus.BadRequest,
                    [new ValidationFailure(nameof(canonicalContest.ContestId), "Matchup could not be found")]);
            }

            canonicalContest.IsLocked = canonicalContest.StartDateUtc.AddMinutes(-5) <= DateTime.UtcNow;
            canonicalContest.WinnerFranchiseSeasonId = canonicalContest.AwayScore > canonicalContest.HomeScore
                ? canonicalContest.AwayFranchiseSeasonId
                : canonicalContest.HomeScore > canonicalContest.AwayScore
                    ? canonicalContest.HomeFranchiseSeasonId
                    : null; // Tie - no winner

            // Determine spread winner based on the matchup spread
            if (matchup is { AwaySpread: not null, HomeSpread: not null })
            {
                var spreadDifference = (canonicalContest.AwayScore + matchup.AwaySpread.Value) - canonicalContest.HomeScore;
                if (spreadDifference > 0)
                    canonicalContest.SpreadWinnerFranchiseSeasonId = canonicalContest.AwayFranchiseSeasonId;
                else if (spreadDifference < 0)
                    canonicalContest.SpreadWinnerFranchiseSeasonId = canonicalContest.HomeFranchiseSeasonId;
                else
                    canonicalContest.SpreadWinnerFranchiseSeasonId = null; // Push
            }
            else
            {
                canonicalContest.SpreadWinnerFranchiseSeasonId = null; // No spread
            }
        }

        result.Contests = canonicalContests.OrderBy(x => x.StartDateUtc)
            .Select(x => new LeagueWeekMatchupResultDto(x)
            {
                LeagueWinnerFranchiseSeasonId = x.SpreadWinnerFranchiseSeasonId ?? x.WinnerFranchiseSeasonId
            }).ToList();

        foreach (var member in league.Members.OrderBy(x => x.User.DisplayName))
        {
            var userPicksQuery = new GetUserPicksByGroupAndWeekQuery
            {
                UserId = member.UserId,
                GroupId = query.LeagueId,
                WeekNumber = query.Week
            };
            var userPicksResult = await _userPicksQueryHandler.ExecuteAsync(userPicksQuery, cancellationToken);

            if (userPicksResult.IsSuccess)
            {
                result.UserPicks.AddRange(userPicksResult.Value);
            }
            else
            {
                _logger.LogWarning(
                    "Could not retrieve user picks for user {UserId} in league {LeagueId} week {Week}",
                    member.UserId, query.LeagueId, query.Week);
            }
        }

        return new Success<LeagueWeekOverviewDto>(result);
    }
}
