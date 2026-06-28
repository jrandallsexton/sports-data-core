using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SportsData.Api.Application.UI.Map.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

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
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetMapMatchupsQueryHandler(
        ILogger<GetMapMatchupsQueryHandler> logger,
        AppDataContext dataContext,
        IContestClientFactory contestClientFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _contestClientFactory = contestClientFactory;
        _dateTimeProvider = dateTimeProvider;
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

        // The map is league-scoped: the league determines which sport's
        // Producer to query. Without a resolvable league we can't pick a
        // ContestClient, so a missing/unknown LeagueId is NotFound.
        var league = await _dataContext.PickemGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.LeagueId, cancellationToken);

        if (league == null)
        {
            return new Failure<GetMapMatchupsResponse>(new GetMapMatchupsResponse(), ResultStatus.NotFound, [new ValidationFailure("LeagueId", "League Not Found")]);
        }

        var client = _contestClientFactory.Resolve(league.Sport);

        Result<List<Matchup>> matchupsResult;
        if (query.WeekNumber is not null)
        {
            var seasonYear = query.SeasonYear ?? _dateTimeProvider.UtcNow().Year;
            matchupsResult = await client.GetMatchupsForSeasonWeek(seasonYear, query.WeekNumber.Value, cancellationToken);
        }
        else
        {
            matchupsResult = await client.GetMatchupsForCurrentWeek(cancellationToken);
        }

        var matchups = matchupsResult.IsSuccess ? matchupsResult.Value : [];

        return new Success<GetMapMatchupsResponse>(new GetMapMatchupsResponse
        {
            Matchups = matchups
        });
    }
}
