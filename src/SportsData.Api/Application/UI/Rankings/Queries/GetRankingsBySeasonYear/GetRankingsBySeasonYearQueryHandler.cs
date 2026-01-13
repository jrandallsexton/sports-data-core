using FluentValidation.Results;

using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Api.Extensions;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.Rankings.Queries.GetRankingsBySeasonYear;

public interface IGetRankingsBySeasonYearQueryHandler
{
    Task<Result<List<RankingsByPollIdByWeekDto>>> ExecuteAsync(
        GetRankingsBySeasonYearQuery query,
        CancellationToken cancellationToken = default);
}

public class GetRankingsBySeasonYearQueryHandler : IGetRankingsBySeasonYearQueryHandler
{
    private readonly ILogger<GetRankingsBySeasonYearQueryHandler> _logger;
    private readonly IFranchiseClientFactory _franchiseClientFactory;

    public GetRankingsBySeasonYearQueryHandler(
        ILogger<GetRankingsBySeasonYearQueryHandler> logger,
        IFranchiseClientFactory franchiseClientFactory)
    {
        _logger = logger;
        _franchiseClientFactory = franchiseClientFactory;
    }

    public async Task<Result<List<RankingsByPollIdByWeekDto>>> ExecuteAsync(
        GetRankingsBySeasonYearQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetRankingsBySeasonYear called with seasonYear={SeasonYear}, Sport={Sport}",
            query.SeasonYear,
            query.Sport);

        try
        {
            var client = _franchiseClientFactory.Resolve(query.Sport);
            var polls = await client.GetFranchiseSeasonRankings(query.SeasonYear, cancellationToken);

            _logger.LogInformation(
                "Received {Count} polls from FranchiseClient for seasonYear={SeasonYear}",
                polls?.Count ?? 0,
                query.SeasonYear);

            if (polls == null || polls.Count == 0)
            {
                _logger.LogWarning(
                    "No polls returned from FranchiseClient for seasonYear={SeasonYear}",
                    query.SeasonYear);
                return new Success<List<RankingsByPollIdByWeekDto>>([]);
            }

            var dtos = polls.Select(poll => poll.ToRankingsByPollDto()).ToList();

            _logger.LogInformation(
                "Successfully transformed {Count} polls to DTOs for seasonYear={SeasonYear}",
                dtos.Count,
                query.SeasonYear);

            return new Success<List<RankingsByPollIdByWeekDto>>(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error in GetRankingsBySeasonYear for seasonYear={SeasonYear}",
                query.SeasonYear);

            return new Failure<List<RankingsByPollIdByWeekDto>>(
                [],
                ResultStatus.Error,
                [new ValidationFailure(nameof(query.SeasonYear), "Error retrieving rankings. Please try again later.")]);
        }
    }
}
