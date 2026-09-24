using Dapper;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

namespace SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonPreviewStats;

public interface IGetFranchiseSeasonPreviewStatsQueryHandler
{
    Task<Result<FranchiseSeasonModelStatsDto>> ExecuteAsync(
        GetFranchiseSeasonPreviewStatsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The compact stat block for the matchup-preview prompt. The SQL returns
/// every stored ESPN team-season statistic; <see cref="FranchiseSeasonModelStatsMapper"/>
/// picks and derives the handful the prompt wants (and is where the tests
/// live, since Dapper cannot run against the in-memory provider).
/// </summary>
public class GetFranchiseSeasonPreviewStatsQueryHandler : IGetFranchiseSeasonPreviewStatsQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ILogger<GetFranchiseSeasonPreviewStatsQueryHandler> _logger;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetFranchiseSeasonPreviewStatsQueryHandler(
        TeamSportDataContext dbContext,
        ILogger<GetFranchiseSeasonPreviewStatsQueryHandler> logger,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<FranchiseSeasonModelStatsDto>> ExecuteAsync(
        GetFranchiseSeasonPreviewStatsQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = _sqlProvider.GetFranchiseSeasonPreviewStats();
        var connection = _dbContext.Database.GetDbConnection();

        var rawStats = (await connection.QueryAsync<FranchiseSeasonRawStat>(
            new CommandDefinition(
                sql,
                new { query.FranchiseSeasonId },
                cancellationToken: cancellationToken))).ToList();

        if (rawStats.Count == 0)
        {
            _logger.LogWarning(
                "Stats not found for FranchiseSeasonId={FranchiseSeasonId}",
                query.FranchiseSeasonId);

            return new Success<FranchiseSeasonModelStatsDto>(new FranchiseSeasonModelStatsDto());
        }

        var mapped = FranchiseSeasonModelStatsMapper.Map(rawStats);

        if (mapped.RushingYardsPerGame is null)
        {
            // The caller's hasStats check keys on this field; say why it is
            // missing while the rows are in front of us.
            _logger.LogWarning(
                "Stats present ({Count} rows) but no rushingYardsPerGame/rushingYards for FranchiseSeasonId={FranchiseSeasonId}; preview will run the no-stats prompt.",
                rawStats.Count, query.FranchiseSeasonId);
        }

        return new Success<FranchiseSeasonModelStatsDto>(mapped);
    }
}
