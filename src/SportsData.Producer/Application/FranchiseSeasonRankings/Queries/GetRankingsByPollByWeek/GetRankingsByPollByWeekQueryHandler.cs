using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

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

    public GetRankingsByPollByWeekQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    private const string Sql = """
        SELECT
            fs."Id" as "FranchiseSeasonId",
            fsl."Uri" as "FranchiseLogoUrl",
            fs."Slug" as "FranchiseSlug",
            fs."DisplayNameShort" as "FranchiseName",
            fs."Wins",
            fs."Losses",
            fsrd."Current" as "Rank",
            fsrd."Previous" as "PreviousRank",
            fsrd."Points",
            fsrd."FirstPlaceVotes",
            fsrd."Trend",
            fsrd."Date" as "PollDateUtc"
        FROM public."FranchiseSeasonRankingDetail" fsrd
        INNER JOIN public."FranchiseSeasonRanking" fsr on fsr."Id" = fsrd."FranchiseSeasonRankingId"
        INNER JOIN public."FranchiseSeason" fs on fs."Id" = fsr."FranchiseSeasonId"
        LEFT JOIN LATERAL (
          SELECT fsl."Uri"
          FROM public."FranchiseSeasonLogo" fsl
          WHERE fsl."FranchiseSeasonId" = fs."Id"
          ORDER BY fsl."Uri"
          LIMIT 1
        ) as fsl on true
        INNER JOIN public."SeasonWeek" sw on sw."Id" = fsr."SeasonWeekId"
        INNER JOIN public."Season" s on s."Id" = sw."SeasonId"
        WHERE fsr."Type" = @PollType and sw."Number" = @WeekNumber and s."Year" = @SeasonYear
        ORDER BY fsrd."Current" asc
        """;

    public async Task<Result<RankingsByPollIdByWeekDto>> ExecuteAsync(
        GetRankingsByPollByWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();

        var entries = (await connection.QueryAsync<RankingsByPollIdByWeekDto.RankingsByPollIdByWeekEntryDto>(
            new CommandDefinition(
                Sql,
                new { query.PollType, query.WeekNumber, query.SeasonYear },
                cancellationToken: cancellationToken))).ToList();

        var result = new RankingsByPollIdByWeekDto
        {
            PollName = query.PollType,
            PollId = query.PollType,
            SeasonYear = query.SeasonYear,
            Week = query.WeekNumber,
            PollDateUtc = entries.FirstOrDefault()?.PollDateUtc ?? DateTime.MinValue,
            Entries = entries,
        };

        return new Success<RankingsByPollIdByWeekDto>(result);
    }
}
