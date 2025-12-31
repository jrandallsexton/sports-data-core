using FluentValidation.Results;

using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Rankings.Queries.GetRankingsByPollWeek;

public interface IGetRankingsByPollWeekQueryHandler
{
    Task<Result<RankingsByPollIdByWeekDto>> ExecuteAsync(
        GetRankingsByPollWeekQuery query,
        CancellationToken cancellationToken = default);
}

public class GetRankingsByPollWeekQueryHandler : IGetRankingsByPollWeekQueryHandler
{
    private readonly ILogger<GetRankingsByPollWeekQueryHandler> _logger;
    private readonly IProvideCanonicalData _canonicalDataProvider;

    private const string PollIdAp = "ap";
    private const string PollIdCoaches = "usa";
    private const string PollIdCfp = "cfp";

    public GetRankingsByPollWeekQueryHandler(
        ILogger<GetRankingsByPollWeekQueryHandler> logger,
        IProvideCanonicalData canonicalDataProvider)
    {
        _logger = logger;
        _canonicalDataProvider = canonicalDataProvider;
    }

    public async Task<Result<RankingsByPollIdByWeekDto>> ExecuteAsync(
        GetRankingsByPollWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        var poll = string.IsNullOrEmpty(query.Poll) ? PollIdAp : query.Poll.ToLowerInvariant();

        if (query.SeasonYear is < 1900 or > 2100)
        {
            return new Failure<RankingsByPollIdByWeekDto>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(query.SeasonYear), "Season year must be between 1900 and 2100")]);
        }

        if (query.Week is < 1 or > 20)
        {
            return new Failure<RankingsByPollIdByWeekDto>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(query.Week), "Season week must be between 1 and 20")]);
        }

        try
        {
            var rankings = await _canonicalDataProvider
                .GetRankingsByPollIdByWeek(poll, query.SeasonYear, query.Week);

            if (rankings == null)
            {
                _logger.LogWarning("No rankings found for season {SeasonYear} week {Week}", query.SeasonYear, query.Week);
                return new Failure<RankingsByPollIdByWeekDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure("rankings", $"No rankings found for season {query.SeasonYear} week {query.Week}")]);
            }

            if (rankings.Entries == null || rankings.Entries.Count == 0)
            {
                _logger.LogInformation("Rankings found but no entries for season {SeasonYear} week {Week}", query.SeasonYear, query.Week);
            }

            rankings = poll switch
            {
                PollIdAp => rankings with
                {
                    PollName = $"AP Top 25 - Week {query.Week}",
                    HasFirstPlaceVotes = true,
                    HasPoints = true,
                    HasTrends = true
                },
                PollIdCoaches => rankings with
                {
                    PollName = $"Coaches Poll - Week {query.Week}",
                    HasFirstPlaceVotes = true,
                    HasPoints = true,
                    HasTrends = true
                },
                PollIdCfp => rankings with
                {
                    PollName = $"College Football Playoffs - Week {query.Week}"
                },
                _ => rankings
            };

            return new Success<RankingsByPollIdByWeekDto>(rankings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rankings for season {SeasonYear} week {Week}", query.SeasonYear, query.Week);
            return new Failure<RankingsByPollIdByWeekDto>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("rankings", "Error retrieving rankings. Please try again later.")]);
        }
    }
}
