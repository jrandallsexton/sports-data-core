using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SportsData.Api.Application.UI.Map.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Map.Queries.GetMapMatchups;

public interface IGetMapMatchupsQueryHandler
{
    Task<Result<GetMapMatchupsResponse>> ExecuteAsync(
        GetMapMatchupsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMapMatchupsQueryHandler : IGetMapMatchupsQueryHandler
{
    private readonly ILogger<GetMapMatchupsQueryHandler> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IContestClientFactory _contestClientFactory;

    public GetMapMatchupsQueryHandler(
        ILogger<GetMapMatchupsQueryHandler> logger,
        AppDataContext dataContext,
        IContestClientFactory contestClientFactory)
    {
        _logger = logger;
        _dataContext = dataContext;
        _contestClientFactory = contestClientFactory;
    }

    public async Task<Result<GetMapMatchupsResponse>> ExecuteAsync(
        GetMapMatchupsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting map matchups. LeagueId={LeagueId}, SeasonYear={SeasonYear}, WeekNumber={WeekNumber}",
            query.LeagueId,
            query.SeasonYear,
            query.WeekNumber);

        var league = await _dataContext.PickemGroups.FirstOrDefaultAsync(x => x.Id == query.LeagueId, cancellationToken);

        if (league == null)
        {
            return new Failure<GetMapMatchupsResponse>(new GetMapMatchupsResponse(), ResultStatus.NotFound, [new ValidationFailure("LeagueId", "League Not Found")]);
        }

        if (query.LeagueId is null && query.WeekNumber is null)
        {
            // TODO: Honor WeekNumber when LeagueId is provided
            var matchupsResult = await _contestClientFactory.Resolve(league.Sport).GetMatchupsForCurrentWeek(cancellationToken);
            var matchups = matchupsResult.IsSuccess ? matchupsResult.Value : [];

            return new Success<GetMapMatchupsResponse>(new GetMapMatchupsResponse
            {
                Matchups = matchups
            });
        }
        else if (query.LeagueId is null)
        {
            // we have a week, but no league
            var seasonYear = query.SeasonYear ?? DateTime.UtcNow.Year;
            var matchupsResult = await _contestClientFactory.Resolve(league.Sport).GetMatchupsForSeasonWeek(seasonYear, query.WeekNumber!.Value, cancellationToken);
            var matchups = matchupsResult.IsSuccess ? matchupsResult.Value : [];

            return new Success<GetMapMatchupsResponse>(new GetMapMatchupsResponse
            {
                Matchups = matchups
            });
        }
        else
        {
            // we have a league (and optionally a week)
            var matchupsResult = await _contestClientFactory.Resolve(league.Sport).GetMatchupsForCurrentWeek(cancellationToken);
            var matchups = matchupsResult.IsSuccess ? matchupsResult.Value : [];

            return new Success<GetMapMatchupsResponse>(new GetMapMatchupsResponse
            {
                Matchups = matchups
            });
        }
    }
}
