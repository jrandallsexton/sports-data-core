using FluentValidation.Results;

using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Api.Extensions;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Season;

namespace SportsData.Api.Application.UI.Rankings.Queries.GetRankingsByPollSeasonWeekId;

public interface IGetRankingsByPollSeasonWeekIdQueryHandler
{
    Task<Result<RankingsByPollIdByWeekDto>> ExecuteAsync(
        GetRankingsByPollSeasonWeekIdQuery query,
        CancellationToken cancellationToken = default);
}

public class GetRankingsByPollSeasonWeekIdQueryHandler : IGetRankingsByPollSeasonWeekIdQueryHandler
{
    private readonly ILogger<GetRankingsByPollSeasonWeekIdQueryHandler> _logger;
    private readonly ISeasonClientFactory _seasonClientFactory;

    public GetRankingsByPollSeasonWeekIdQueryHandler(
        ILogger<GetRankingsByPollSeasonWeekIdQueryHandler> logger,
        ISeasonClientFactory seasonClientFactory)
    {
        _logger = logger;
        _seasonClientFactory = seasonClientFactory;
    }

    public async Task<Result<RankingsByPollIdByWeekDto>> ExecuteAsync(
        GetRankingsByPollSeasonWeekIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var poll = string.IsNullOrEmpty(query.Poll) ? "ap" : query.Poll.ToLowerInvariant();

        _logger.LogInformation(
            "GetRankingsByPollSeasonWeekId: SeasonWeekId={SeasonWeekId}, Poll={Poll}",
            query.SeasonWeekId, poll);

        var seasonClient = _seasonClientFactory.Resolve(query.Sport);
        var result = await seasonClient.GetPollBySeasonWeekId(query.SeasonWeekId, poll, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "No rankings found for SeasonWeekId={SeasonWeekId} poll={Poll}",
                query.SeasonWeekId, poll);

            var errors = result is Failure<FranchiseSeasonPollDto> failure
                ? failure.Errors
                : [new ValidationFailure("rankings", $"No rankings found for season week {query.SeasonWeekId}")];

            return new Failure<RankingsByPollIdByWeekDto>(
                default!,
                result.Status,
                errors);
        }

        var dto = result.Value.ToRankingsByPollDto();

        return new Success<RankingsByPollIdByWeekDto>(dto);
    }
}
