using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetContestPreviewHistory;

public interface IGetContestPreviewHistoryQueryHandler
{
    Task<Result<ContestPreviewHistoryDto>> ExecuteAsync(
        GetContestPreviewHistoryQuery query,
        CancellationToken cancellationToken = default);
}

public class GetContestPreviewHistoryQueryHandler : IGetContestPreviewHistoryQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetContestPreviewHistoryQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    /// <summary>
    /// Dapper row for the prior-season query: a PreviewGameResultDto plus
    /// which TARGET team (Away/Home) the row belongs to.
    /// </summary>
    private class PriorSeasonRow : PreviewGameResultDto
    {
        public string Side { get; set; } = default!;
    }

    public async Task<Result<ContestPreviewHistoryDto>> ExecuteAsync(
        GetContestPreviewHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();

        var headToHead = (await connection.QueryAsync<PreviewGameResultDto>(
            new CommandDefinition(
                _sqlProvider.GetContestHeadToHeadResults(),
                new { query.ContestId, Count = query.MeetingCount },
                cancellationToken: cancellationToken))).ToList();

        var priorSeasonRows = (await connection.QueryAsync<PriorSeasonRow>(
            new CommandDefinition(
                _sqlProvider.GetContestPriorSeasonResults(),
                new { query.ContestId, Count = query.RecentGameCount },
                cancellationToken: cancellationToken))).ToList();

        // An unknown contest yields empty lists everywhere (the target CTE
        // matches nothing) — an empty history is a normal state for a
        // first-ever meeting, so no NotFound here; the caller degrades
        // gracefully either way.
        var dto = new ContestPreviewHistoryDto
        {
            HeadToHead = headToHead,
            AwayPriorSeasonGames = priorSeasonRows
                .Where(x => x.Side == "Away").Cast<PreviewGameResultDto>().ToList(),
            HomePriorSeasonGames = priorSeasonRows
                .Where(x => x.Side == "Home").Cast<PreviewGameResultDto>().ToList()
        };

        return new Success<ContestPreviewHistoryDto>(dto);
    }
}
