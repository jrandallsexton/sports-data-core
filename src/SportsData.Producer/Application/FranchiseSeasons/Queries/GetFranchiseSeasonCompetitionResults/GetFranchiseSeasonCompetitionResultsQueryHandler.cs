using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

using System.Collections.Generic;
using System.Linq;

namespace SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonCompetitionResults;

public interface IGetFranchiseSeasonCompetitionResultsQueryHandler
{
    Task<Result<List<FranchiseSeasonCompetitionResultDto>>> ExecuteAsync(
        GetFranchiseSeasonCompetitionResultsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetFranchiseSeasonCompetitionResultsQueryHandler : IGetFranchiseSeasonCompetitionResultsQueryHandler
{
    private readonly TeamSportDataContext _dbContext;

    public GetFranchiseSeasonCompetitionResultsQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    private const string Sql = """
        SELECT
          c."StartDateUtc",
          c."Id" AS "ContestId",
          fAway."Abbreviation" as "AwayShort",
          fsAway."Id" as "AwayFranchiseSeasonId",
          fAway."Slug" as "AwaySlug",
          fsrdAway."Current" as "AwayRank",
          fHome."Abbreviation" as "HomeShort",
          fsHome."Id" as "HomeFranchiseSeasonId",
          fHome."Slug" as "HomeSlug",
          fsrdHome."Current" as "HomeRank",
          co."Details" as "Spread",
          (co."Spread" * -1) as "AwaySpread",
          co."Spread" as "HomeSpread",
          co."OverUnder" as "OverUnder",
          c."FinalizedUtc",
          c."AwayScore",
          c."HomeScore",
          c."WinnerFranchiseId" as "WinnerFranchiseSeasonId",
          c."SpreadWinnerFranchiseId" as "SpreadWinnerFranchiseSeasonId",
          c."OverUnder" as "OverUnderResult",
          c."EndDateUtc" as "CompletedUtc"
        FROM public."Contest" c
        INNER JOIN public."Venue" v on v."Id" = c."VenueId"
        INNER JOIN public."Competition" comp on comp."ContestId" = c."Id"
        LEFT JOIN LATERAL (
          SELECT *
          FROM public."CompetitionOdds"
          WHERE "CompetitionId" = comp."Id"
            AND "ProviderId" IN ('{SportsData.Producer.Application.Contests.Queries.Matchups.MatchupSqlBuilder.PreferredOddsProviderId}', '{SportsData.Producer.Application.Contests.Queries.Matchups.MatchupSqlBuilder.FallbackOddsProviderId}')
          ORDER BY CASE WHEN "ProviderId" = '{SportsData.Producer.Application.Contests.Queries.Matchups.MatchupSqlBuilder.PreferredOddsProviderId}' THEN 1 ELSE 2 END
          LIMIT 1
        ) co ON TRUE
        INNER JOIN public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
        INNER JOIN public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"
        INNER JOIN public."GroupSeason" gsAway on gsAway."Id" = fsAway."GroupSeasonId"
        LEFT JOIN public."FranchiseSeasonRanking" fsrAway on fsrAway."FranchiseSeasonId" = fsAway."Id" and
                fsrAway."DefaultRanking" = true and fsrAway."Type" in ('ap', 'cfp') and
                fsrAway."SeasonWeekId" = c."SeasonWeekId"
        LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdAway on fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
        INNER JOIN public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
        INNER JOIN public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"
        INNER JOIN public."GroupSeason" gsHome on gsHome."Id" = fsHome."GroupSeasonId"
        LEFT JOIN public."FranchiseSeasonRanking" fsrHome on fsrHome."FranchiseSeasonId" = fsHome."Id" and
                fsrHome."DefaultRanking" = true and fsrHome."Type" in ('ap', 'cfp') and
                fsrHome."SeasonWeekId" = c."SeasonWeekId"
        LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdHome on fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
        WHERE c."StartDateUtc" <= NOW() AND ((fsAway."Id" = @FranchiseSeasonId) OR (fsHome."Id" = @FranchiseSeasonId))
        ORDER BY c."StartDateUtc", fHome."Slug"
        """;

    public async Task<Result<List<FranchiseSeasonCompetitionResultDto>>> ExecuteAsync(
        GetFranchiseSeasonCompetitionResultsQuery query,
        CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();

        var results = (await connection.QueryAsync<FranchiseSeasonCompetitionResultDto>(
            new CommandDefinition(
                Sql,
                new { query.FranchiseSeasonId },
                cancellationToken: cancellationToken))).ToList();

        return new Success<List<FranchiseSeasonCompetitionResultDto>>(results);
    }
}
