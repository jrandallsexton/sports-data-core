using FluentValidation.Results;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.Mapping;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.Admin.Queries.GetMatchupForContest;

public interface IGetMatchupForContestQueryHandler
{
    Task<Result<LeagueWeekMatchupsDto.MatchupForPickDto>> ExecuteAsync(
        GetMatchupForContestQuery query,
        CancellationToken cancellationToken);
}

public class GetMatchupForContestQueryHandler : IGetMatchupForContestQueryHandler
{
    private readonly IContestClientFactory _contestClientFactory;
    private readonly ILogger<GetMatchupForContestQueryHandler> _logger;

    public GetMatchupForContestQueryHandler(
        IContestClientFactory contestClientFactory,
        ILogger<GetMatchupForContestQueryHandler> logger)
    {
        _contestClientFactory = contestClientFactory;
        _logger = logger;
    }

    public async Task<Result<LeagueWeekMatchupsDto.MatchupForPickDto>> ExecuteAsync(
        GetMatchupForContestQuery query,
        CancellationToken cancellationToken)
    {
        // Admin debug endpoint — no user context, so default to Roundel.
        var matchupsResult = await _contestClientFactory
            .Resolve(query.Sport)
            .GetMatchupsByContestIds(new List<Guid> { query.ContestId }, MarkDirection.Roundel, cancellationToken);

        if (!matchupsResult.IsSuccess)
        {
            _logger.LogError(
                "Producer GetMatchupsByContestIds failed for ContestId={ContestId}, Sport={Sport}",
                query.ContestId, query.Sport);
            return new Failure<LeagueWeekMatchupsDto.MatchupForPickDto>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("matchup", "Failed to retrieve matchup data from Producer")]);
        }

        var canonical = matchupsResult.Value?.FirstOrDefault();
        if (canonical is null)
        {
            return new Failure<LeagueWeekMatchupsDto.MatchupForPickDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("contestId", $"No matchup found for ContestId {query.ContestId}")]);
        }

        return new Success<LeagueWeekMatchupsDto.MatchupForPickDto>(
            MatchupForPickDtoMapper.FromCanonical(canonical));
    }
}
