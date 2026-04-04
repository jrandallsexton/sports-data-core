using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsForCurrentWeek;

public interface IGetMatchupsForCurrentWeekQueryHandler
{
    Task<Result<List<Matchup>>> ExecuteAsync(
        GetMatchupsForCurrentWeekQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMatchupsForCurrentWeekQueryHandler : IGetMatchupsForCurrentWeekQueryHandler
{
    private readonly TeamSportDataContext _dbContext;

    public GetMatchupsForCurrentWeekQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<Matchup>>> ExecuteAsync(
        GetMatchupsForCurrentWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            WITH current_week AS (
              SELECT sw."Id" AS "SeasonWeekId",
                     sw."Number" AS "WeekNumber",
                     s."Id" AS "SeasonId",
                     s."Year" AS "SeasonYear"
              FROM public."Season" s
              JOIN public."SeasonWeek" sw ON sw."SeasonId" = s."Id"
              JOIN public."SeasonPhase" sp ON sp."Id" = sw."SeasonPhaseId"
              WHERE sw."StartDate" <= NOW() AND sw."EndDate" > NOW()
              ORDER BY sw."StartDate"
              LIMIT 1
            )
            SELECT
              cw."SeasonWeekId",
              cw."SeasonYear" AS "SeasonYear",
              cw."WeekNumber" AS "SeasonWeek",
              c."Id" AS "ContestId",
              cn."Headline" AS "Headline",
              c."StartDateUtc" AS "StartDateUtc",
              cs."StatusTypeName" AS "Status",
              v."Name" AS "VenueName", v."City" AS "VenueCity", v."State" AS "VenueState",
              v."Latitude" AS "VenueLatitude", v."Longitude" AS "VenueLongitude",
              fAway."Slug" AS "AwaySlug", fAway."ColorCodeHex" AS "AwayColor",
              fAway."Abbreviation" AS "AwayAbbreviation", fsrdAway."Current" AS "AwayRank",
              fsAway."Wins" AS "AwayWins", fsAway."Losses" AS "AwayLosses",
              fsAway."ConferenceWins" AS "AwayConferenceWins", fsAway."ConferenceLosses" AS "AwayConferenceLosses",
              gsAway."Slug" AS "AwayConferenceSlug", fsAway."GroupSeasonMap" AS "AwayGroupSeasonMap",
              fHome."Slug" AS "HomeSlug", fHome."ColorCodeHex" AS "HomeColor",
              fHome."Abbreviation" AS "HomeAbbreviation", fsrdHome."Current" AS "HomeRank",
              fsHome."Wins" AS "HomeWins", fsHome."Losses" AS "HomeLosses",
              fsHome."ConferenceWins" AS "HomeConferenceWins", fsHome."ConferenceLosses" AS "HomeConferenceLosses",
              gsHome."Slug" AS "HomeConferenceSlug", fsHome."GroupSeasonMap" AS "HomeGroupSeasonMap",
              co."Details" AS "Spread", (co."Spread" * -1) AS "AwaySpread",
              co."Spread" AS "HomeSpread", co."OverUnder", co."OverOdds", co."UnderOdds"
            FROM current_week cw
            INNER JOIN public."Contest" c ON c."SeasonWeekId" = cw."SeasonWeekId"
            {MatchupSqlBuilder.MatchupJoins}
            {MatchupSqlBuilder.RankingJoinsBySeasonWeek}
            {MatchupSqlBuilder.MatchupOrderBy}
            """;

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryAsync<Matchup>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return new Success<List<Matchup>>(result.ToList());
    }
}
