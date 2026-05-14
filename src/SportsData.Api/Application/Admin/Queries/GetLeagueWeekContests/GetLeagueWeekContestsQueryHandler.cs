using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetLeagueWeekContests;

public interface IGetLeagueWeekContestsQueryHandler
{
    Task<Result<GetLeagueWeekContestsResult>> ExecuteAsync(
        GetLeagueWeekContestsQuery query,
        CancellationToken cancellationToken);
}

public class GetLeagueWeekContestsQueryHandler : IGetLeagueWeekContestsQueryHandler
{
    private readonly AppDataContext _dbContext;
    private readonly ILogger<GetLeagueWeekContestsQueryHandler> _logger;

    public GetLeagueWeekContestsQueryHandler(
        AppDataContext dbContext,
        ILogger<GetLeagueWeekContestsQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<GetLeagueWeekContestsResult>> ExecuteAsync(
        GetLeagueWeekContestsQuery query,
        CancellationToken cancellationToken)
    {
        var sport = await _dbContext.PickemGroups
            .AsNoTracking()
            .Where(x => x.Id == query.LeagueId)
            .Select(x => (Sport?)x.Sport)
            .FirstOrDefaultAsync(cancellationToken);

        if (sport is null)
        {
            _logger.LogWarning(
                "GetLeagueWeekContests: league not found. LeagueId={LeagueId}, Week={Week}",
                query.LeagueId, query.Week);
            return new Failure<GetLeagueWeekContestsResult>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.LeagueId), "League not found")]);
        }

        var contestIds = await _dbContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(x => x.GroupId == query.LeagueId && x.SeasonWeek == query.Week)
            .Select(x => x.ContestId)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "GetLeagueWeekContests: resolved {Count} contest IDs. LeagueId={LeagueId}, Week={Week}, Sport={Sport}",
            contestIds.Count, query.LeagueId, query.Week, sport);

        return new Success<GetLeagueWeekContestsResult>(
            new GetLeagueWeekContestsResult(sport.Value, contestIds));
    }
}
