using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

using System;
using System.Linq;

namespace SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetRankingsByPollByWeek;

public interface IGetRankingsByPollByWeekQueryHandler
{
    Task<Result<RankingsByPollIdByWeekDto>> ExecuteAsync(
        GetRankingsByPollByWeekQuery query,
        CancellationToken cancellationToken = default);
}

public class GetRankingsByPollByWeekQueryHandler : IGetRankingsByPollByWeekQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetRankingsByPollByWeekQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<RankingsByPollIdByWeekDto>> ExecuteAsync(
        GetRankingsByPollByWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = _sqlProvider.GetRankingsByPollByWeek();
        var connection = _dbContext.Database.GetDbConnection();

        var entries = (await connection.QueryAsync<RankingsByPollIdByWeekDto.RankingsByPollIdByWeekEntryDto>(
            new CommandDefinition(
                sql,
                new { query.PollType, query.WeekNumber, query.SeasonYear },
                cancellationToken: cancellationToken))).ToList();

        if (entries.Count == 0)
        {
            return new Failure<RankingsByPollIdByWeekDto>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure("Rankings",
                    $"No rankings found for poll={query.PollType}, season={query.SeasonYear}, week={query.WeekNumber}")]);
        }

        var result = new RankingsByPollIdByWeekDto
        {
            PollName = query.PollType,
            PollId = query.PollType,
            SeasonYear = query.SeasonYear,
            Week = query.WeekNumber,
            PollDateUtc = entries.First().PollDateUtc ?? DateTime.MinValue,
            Entries = entries,
        };

        return new Success<RankingsByPollIdByWeekDto>(result);
    }
}
