using SportsData.Api.Application.UI.Map.Dtos;
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
    private readonly IContestClientFactory _contestClientFactory;

    public GetMapMatchupsQueryHandler(
        ILogger<GetMapMatchupsQueryHandler> logger,
        IContestClientFactory contestClientFactory)
    {
        _logger = logger;
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

        if (query.LeagueId is null && query.WeekNumber is null)
        {
            // TODO: Honor WeekNumber when LeagueId is provided
            var matchupsResult = await _contestClientFactory.Resolve(SportsData.Core.Common.Sport.FootballNcaa).GetMatchupsForCurrentWeek();
            var matchups = matchupsResult.IsSuccess ? matchupsResult.Value : new System.Collections.Generic.List<SportsData.Core.Dtos.Canonical.Matchup>();

            return new Success<GetMapMatchupsResponse>(new GetMapMatchupsResponse
            {
                Matchups = matchups
            });
        }
        else if (query.LeagueId is null)
        {
            // we have a week, but no league
            var seasonYear = query.SeasonYear ?? DateTime.UtcNow.Year;
            var matchupsResult = await _contestClientFactory.Resolve(SportsData.Core.Common.Sport.FootballNcaa).GetMatchupsForSeasonWeek(seasonYear, query.WeekNumber!.Value);
            var matchups = matchupsResult.IsSuccess ? matchupsResult.Value : new System.Collections.Generic.List<SportsData.Core.Dtos.Canonical.Matchup>();

            return new Success<GetMapMatchupsResponse>(new GetMapMatchupsResponse
            {
                Matchups = matchups
            });
        }
        else
        {
            // we have a league (and optionally a week)
            var matchupsResult = await _contestClientFactory.Resolve(SportsData.Core.Common.Sport.FootballNcaa).GetMatchupsForCurrentWeek();
            var matchups = matchupsResult.IsSuccess ? matchupsResult.Value : new System.Collections.Generic.List<SportsData.Core.Dtos.Canonical.Matchup>();

            return new Success<GetMapMatchupsResponse>(new GetMapMatchupsResponse
            {
                Matchups = matchups
            });
        }
    }
}
