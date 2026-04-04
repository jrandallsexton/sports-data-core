using SportsData.Producer.Application.Contests.Queries.Matchups;
using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsByContestIds;

public interface IGetMatchupsByContestIdsQueryHandler
{
    Task<Result<List<LeagueMatchupDto>>> ExecuteAsync(
        GetMatchupsByContestIdsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMatchupsByContestIdsQueryHandler : IGetMatchupsByContestIdsQueryHandler
{
    private readonly TeamSportDataContext _dbContext;

    public GetMatchupsByContestIdsQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<LeagueMatchupDto>>> ExecuteAsync(
        GetMatchupsByContestIdsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.ContestIds.Length == 0)
        {
            return new Success<List<LeagueMatchupDto>>(new List<LeagueMatchupDto>());
        }

        string sql = """
            SELECT
              c."SeasonWeekId" AS "SeasonWeekId",
              c."Id" AS "ContestId",
              c."StartDateUtc" AS "StartDateUtc",
              REPLACE(cs."StatusDescription", ' ', '') AS "Status",
              STRING_AGG(cb."MediaName", ' | ') AS "Broadcasts",
              v."Name" AS "Venue", v."City" AS "VenueCity", v."State" AS "VenueState",
              fAway."DisplayName" AS "Away", fAway."Abbreviation" AS "AwayShort",
              fsAway."Id" AS "AwayFranchiseSeasonId", flAway."Uri" AS "AwayLogoUri",
              fAway."Slug" AS "AwaySlug", fAway."ColorCodeHex" AS "AwayColor",
              fsrdAway."Current" AS "AwayRank", gsAway."Slug" AS "AwayConferenceSlug",
              fsAway."Wins" AS "AwayWins", fsAway."Losses" AS "AwayLosses",
              fsAway."ConferenceWins" AS "AwayConferenceWins", fsAway."ConferenceLosses" AS "AwayConferenceLosses",
              fHome."DisplayName" AS "Home", fHome."Abbreviation" AS "HomeShort",
              fsHome."Id" AS "HomeFranchiseSeasonId", flHome."Uri" AS "HomeLogoUri",
              fHome."Slug" AS "HomeSlug", fHome."ColorCodeHex" AS "HomeColor",
              fsrdHome."Current" AS "HomeRank", gsHome."Slug" AS "HomeConferenceSlug",
              fsHome."Wins" AS "HomeWins", fsHome."Losses" AS "HomeLosses",
              fsHome."ConferenceWins" AS "HomeConferenceWins", fsHome."ConferenceLosses" AS "HomeConferenceLosses",
              co."Details" AS "SpreadCurrentDetails", co."Spread" AS "SpreadCurrent",
              cto."SpreadPointsOpen" AS "SpreadOpen",
              co."OverUnder" AS "OverUnderCurrent", co."TotalPointsOpen" AS "OverUnderOpen",
              co."OverOdds", co."UnderOdds",
              c."AwayScore", c."HomeScore",
              c."WinnerFranchiseId" AS "WinnerFranchiseSeasonId",
              c."SpreadWinnerFranchiseId" AS "SpreadWinnerFranchiseSeasonId",
              c."OverUnder" AS "OverUnderResult",
              c."EndDateUtc" AS "CompletedUtc"
            FROM public."Contest" c
            INNER JOIN public."Venue" v ON v."Id" = c."VenueId"
            INNER JOIN public."Competition" comp ON comp."ContestId" = c."Id"
            LEFT JOIN public."CompetitionBroadcast" cb ON cb."CompetitionId" = comp."Id"
            LEFT JOIN public."CompetitionStatus" cs ON cs."CompetitionId" = comp."Id"
            LEFT JOIN LATERAL (
              SELECT * FROM public."CompetitionOdds"
              WHERE "CompetitionId" = comp."Id" AND "ProviderId" IN ('{MatchupSqlBuilder.PreferredOddsProviderId}', '{MatchupSqlBuilder.FallbackOddsProviderId}')
              ORDER BY CASE WHEN "ProviderId" = '{MatchupSqlBuilder.PreferredOddsProviderId}' THEN 1 ELSE 2 END
              LIMIT 1
            ) co ON TRUE
            LEFT JOIN public."CompetitionTeamOdds" cto ON cto."CompetitionOddsId" = co."Id" AND cto."Side" = 'Home'
            INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
            INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
            LEFT JOIN LATERAL (
              SELECT fl.* FROM public."FranchiseLogo" fl
              WHERE fl."FranchiseId" = fAway."Id"
              ORDER BY fl."CreatedUtc" ASC LIMIT 1
            ) flAway ON TRUE
            INNER JOIN public."GroupSeason" gsAway ON gsAway."Id" = fsAway."GroupSeasonId"
            LEFT JOIN LATERAL (
              SELECT fsr.* FROM public."FranchiseSeasonRanking" fsr
              INNER JOIN public."SeasonWeek" sw ON sw."Id" = fsr."SeasonWeekId"
              WHERE fsr."FranchiseSeasonId" = fsAway."Id"
                AND fsr."DefaultRanking" = true AND fsr."Type" IN ('ap', 'cfp')
                AND sw."StartDate" <= c."StartDateUtc"
              ORDER BY sw."StartDate" DESC LIMIT 1
            ) fsrAway ON TRUE
            LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdAway ON fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
            INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
            INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
            LEFT JOIN LATERAL (
              SELECT fl.* FROM public."FranchiseLogo" fl
              WHERE fl."FranchiseId" = fHome."Id"
              ORDER BY fl."CreatedUtc" ASC LIMIT 1
            ) flHome ON TRUE
            INNER JOIN public."GroupSeason" gsHome ON gsHome."Id" = fsHome."GroupSeasonId"
            LEFT JOIN LATERAL (
              SELECT fsr.* FROM public."FranchiseSeasonRanking" fsr
              INNER JOIN public."SeasonWeek" sw ON sw."Id" = fsr."SeasonWeekId"
              WHERE fsr."FranchiseSeasonId" = fsHome."Id"
                AND fsr."DefaultRanking" = true AND fsr."Type" IN ('ap', 'cfp')
                AND sw."StartDate" <= c."StartDateUtc"
              ORDER BY sw."StartDate" DESC LIMIT 1
            ) fsrHome ON TRUE
            LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdHome ON fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
            WHERE c."Id" = ANY(@ContestIds)
            GROUP BY
              c."SeasonWeekId", c."Id", c."StartDateUtc", cs."StatusDescription",
              v."Name", v."City", v."State",
              fAway."DisplayName", fAway."DisplayNameShort", fsAway."Id", flAway."Uri", fAway."Slug",
              fsrdAway."Current", gsAway."Slug",
              fsAway."Wins", fsAway."Losses", fsAway."ConferenceWins", fsAway."ConferenceLosses",
              fAway."Abbreviation", fAway."ColorCodeHex",
              fHome."Abbreviation", fHome."ColorCodeHex",
              fHome."DisplayName", fHome."DisplayNameShort", fsHome."Id", flHome."Uri", fHome."Slug",
              fsrdHome."Current", gsHome."Slug",
              fsHome."Wins", fsHome."Losses", fsHome."ConferenceWins", fsHome."ConferenceLosses",
              co."Details", co."Spread", co."OverUnder", co."OverOdds", co."UnderOdds",
              cto."SpreadPointsOpen", co."TotalPointsOpen",
              c."AwayScore", c."HomeScore", c."WinnerFranchiseId", c."SpreadWinnerFranchiseId",
              c."OverUnder", c."EndDateUtc"
            ORDER BY c."StartDateUtc", fHome."Slug"
            """;

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryAsync<LeagueMatchupDto>(
            new CommandDefinition(sql, new { ContestIds = query.ContestIds }, cancellationToken: cancellationToken));

        return new Success<List<LeagueMatchupDto>>(result.ToList());
    }
}
