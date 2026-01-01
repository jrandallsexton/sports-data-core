using FluentValidation.Results;

using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Api.Extensions;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

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
    private readonly IProvideCanonicalData _canonicalDataProvider;

    public GetRankingsBySeasonYearQueryHandler(
        ILogger<GetRankingsBySeasonYearQueryHandler> logger,
        IProvideCanonicalData canonicalDataProvider)
    {
        _logger = logger;
        _canonicalDataProvider = canonicalDataProvider;
    }

    public async Task<Result<List<RankingsByPollIdByWeekDto>>> ExecuteAsync(
        GetRankingsBySeasonYearQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetRankingsBySeasonYear called with seasonYear={SeasonYear}",
            query.SeasonYear);

        try
        {
            var polls = await _canonicalDataProvider.GetFranchiseSeasonRankings(query.SeasonYear);

            _logger.LogInformation(
                "Received {Count} polls from CanonicalDataProvider for seasonYear={SeasonYear}",
                polls?.Count ?? 0,
                query.SeasonYear);

            if (polls == null || polls.Count == 0)
            {
                _logger.LogWarning(
                    "No polls returned from CanonicalDataProvider for seasonYear={SeasonYear}",
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
