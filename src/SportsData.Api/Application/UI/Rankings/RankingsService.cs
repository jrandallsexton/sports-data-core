using FluentValidation.Results;

using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Rankings
{
    public interface IRankingsService
    {
        Task<Result<Dictionary<string, RankingsByPollIdByWeekDto>>> GetPollRankingsByPollWeek(int seasonYear, int week, CancellationToken ct);
        Task<Result<RankingsByPollIdByWeekDto>> GetRankingsByPollWeek(int seasonYear, int week, string poll, CancellationToken ct);
    }

    public class RankingsService : IRankingsService
    {
        private readonly ILogger<RankingsService> _logger;
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly AppDataContext _dataContext;

        public RankingsService(
            ILogger<RankingsService> logger,
            IProvideCanonicalData canonicalDataProvider,
            AppDataContext dataContext)
        {
            _logger = logger;
            _canonicalDataProvider = canonicalDataProvider;
            _dataContext = dataContext;
        }

        public async Task<Result<Dictionary<string, RankingsByPollIdByWeekDto>>> GetPollRankingsByPollWeek(int seasonYear, int week, CancellationToken ct)
        {
            var values = new Dictionary<string, RankingsByPollIdByWeekDto>();

            var cfp = await GetRankingsByPollWeek(seasonYear, week, "cfp", ct);
            if (cfp.IsSuccess)
            {
                values.Add($"College Football Playoff - Week {week}", cfp.Value);
            }

            var ap = await GetRankingsByPollWeek(seasonYear, week, "ap", ct);
            if (ap.IsSuccess)
            {
                values.Add($"AP Top 25 - Week {week}", ap.Value);
            }

            var coaches = await GetRankingsByPollWeek(seasonYear, week, "usa", ct);
            if (coaches.IsSuccess)
            {
                values.Add($"Coaches Poll - Week {week}", coaches.Value);
            }

            return new Success<Dictionary<string, RankingsByPollIdByWeekDto>>(values);

        }

        public async Task<Result<RankingsByPollIdByWeekDto>> GetRankingsByPollWeek(
            int seasonYear,
            int seasonWeek,
            string poll,
            CancellationToken ct)
        {
            poll = string.IsNullOrEmpty(poll) ? "ap" : poll.ToLowerInvariant();

            if (seasonYear is < 1900 or > 2100)
            {
                return new Failure<RankingsByPollIdByWeekDto>(
                    default!,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(seasonYear), "Season year must be between 1900 and 2100")]);
            }

            if (seasonWeek is < 1 or > 20)
            {
                return new Failure<RankingsByPollIdByWeekDto>(
                    default!,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(seasonWeek), "Season week must be between 1 and 20")]);
            }

            try
            {
                var rankings = await _canonicalDataProvider.GetRankingsByPollIdByWeek(
                    poll, seasonYear, seasonWeek);

                if (rankings == null)
                {
                    _logger.LogWarning("No rankings found for season {SeasonYear} week {Week}", seasonYear, seasonWeek);
                    return new Failure<RankingsByPollIdByWeekDto>(
                        default!,
                        ResultStatus.NotFound,
                        [new ValidationFailure("rankings", $"No rankings found for season {seasonYear} week {seasonWeek}")]);
                }

                if (rankings.Entries == null || rankings.Entries.Count == 0)
                {
                    _logger.LogInformation("Rankings found but no entries for season {SeasonYear} week {Week}", seasonYear, seasonWeek);
                }

                return new Success<RankingsByPollIdByWeekDto>(rankings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving rankings for season {SeasonYear} week {Week}", seasonYear, seasonWeek);
                return new Failure<RankingsByPollIdByWeekDto>(
                    default!,
                    ResultStatus.BadRequest,
                    [new ValidationFailure("rankings", $"Error retrieving rankings: {ex.Message}")]);
            }
        }
    }
}
