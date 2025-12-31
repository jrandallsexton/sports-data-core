using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsByPollWeek;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Rankings.Queries.GetPollRankingsByWeek;

public interface IGetPollRankingsByWeekQueryHandler
{
    Task<Result<List<RankingsByPollIdByWeekDto>>> ExecuteAsync(
        GetPollRankingsByWeekQuery query,
        CancellationToken cancellationToken = default);
}

public class GetPollRankingsByWeekQueryHandler : IGetPollRankingsByWeekQueryHandler
{
    private readonly IGetRankingsByPollWeekQueryHandler _rankingsByPollWeekHandler;

    private const string PollIdAp = "ap";
    private const string PollIdCoaches = "usa";
    private const string PollIdCfp = "cfp";

    public GetPollRankingsByWeekQueryHandler(
        IGetRankingsByPollWeekQueryHandler rankingsByPollWeekHandler)
    {
        _rankingsByPollWeekHandler = rankingsByPollWeekHandler;
    }

    public async Task<Result<List<RankingsByPollIdByWeekDto>>> ExecuteAsync(
        GetPollRankingsByWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        var values = new List<RankingsByPollIdByWeekDto>();

        var cfp = await _rankingsByPollWeekHandler.ExecuteAsync(
            new GetRankingsByPollWeekQuery
            {
                SeasonYear = query.SeasonYear,
                Week = query.Week,
                Poll = PollIdCfp
            },
            cancellationToken);

        if (cfp.IsSuccess)
        {
            values.Add(cfp.Value);
        }

        var ap = await _rankingsByPollWeekHandler.ExecuteAsync(
            new GetRankingsByPollWeekQuery
            {
                SeasonYear = query.SeasonYear,
                Week = query.Week,
                Poll = PollIdAp
            },
            cancellationToken);

        if (ap.IsSuccess)
        {
            values.Add(ap.Value);
        }

        var coaches = await _rankingsByPollWeekHandler.ExecuteAsync(
            new GetRankingsByPollWeekQuery
            {
                SeasonYear = query.SeasonYear,
                Week = query.Week,
                Poll = PollIdCoaches
            },
            cancellationToken);

        if (coaches.IsSuccess)
        {
            values.Add(coaches.Value);
        }

        return new Success<List<RankingsByPollIdByWeekDto>>(values);
    }
}
