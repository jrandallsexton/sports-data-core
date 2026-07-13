using FluentValidation.Results;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueGameDates;

public interface IGetLeagueGameDatesQueryHandler
{
    Task<Result<GameDatesDto>> ExecuteAsync(GetLeagueGameDatesQuery query, CancellationToken cancellationToken = default);
}

public class GetLeagueGameDatesQueryHandler : IGetLeagueGameDatesQueryHandler
{
    private readonly ILogger<GetLeagueGameDatesQueryHandler> _logger;
    private readonly IContestClientFactory _contestClientFactory;

    public GetLeagueGameDatesQueryHandler(
        ILogger<GetLeagueGameDatesQueryHandler> logger,
        IContestClientFactory contestClientFactory)
    {
        _logger = logger;
        _contestClientFactory = contestClientFactory;
    }

    public async Task<Result<GameDatesDto>> ExecuteAsync(
        GetLeagueGameDatesQuery query,
        CancellationToken cancellationToken = default)
    {
        // Shared slug→Sport resolution (mirrors POST /ui/leagues/{sport}/{league}).
        Sport mode;
        try
        {
            mode = ModeMapper.ResolveMode(query.Sport, query.League);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex,
                "Unsupported sport/league combination. Sport={Sport}, League={League}",
                query.Sport, query.League);
            return new Failure<GameDatesDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure("Sport/League", ex.Message)]);
        }

        var gameDates = await _contestClientFactory
            .Resolve(mode)
            .GetGameDates(query.From, query.To, cancellationToken);

        if (gameDates is Failure<List<DateOnly>> failure)
            return new Failure<GameDatesDto>(default!, failure.Status, failure.Errors);

        return new Success<GameDatesDto>(new GameDatesDto(gameDates.Value));
    }
}
