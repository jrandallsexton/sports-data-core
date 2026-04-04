using Dapper;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupForPreview;

public interface IGetMatchupForPreviewQueryHandler
{
    Task<Result<MatchupForPreviewDto>> ExecuteAsync(
        GetMatchupForPreviewQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<Dictionary<Guid, MatchupForPreviewDto>>> ExecuteBatchAsync(
        GetMatchupsForPreviewBatchQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMatchupForPreviewQueryHandler : IGetMatchupForPreviewQueryHandler
{
    private readonly TeamSportDataContext _dbContext;

    private static readonly string PreviewSql = """
        SELECT
          c."Sport" AS "Sport",
          sp."Year" AS "SeasonYear",
          sw."Number" AS "WeekNumber",
          c."Id" AS "ContestId",
          cn."Headline" AS "Headline",
          c."StartDateUtc" AS "StartDateUtc",
          cs."StatusTypeName" AS "Status",
          v."Name" AS "Venue", v."City" AS "VenueCity", v."State" AS "VenueState",
          fsAway."Id" AS "AwayFranchiseSeasonId", fAway."DisplayName" AS "Away",
          fAway."Slug" AS "AwaySlug", fsrdAway."Current" AS "AwayRank",
          gsAway."Slug" AS "AwayConferenceSlug", gsAwayParent."Slug" AS "AwayParentConferenceSlug",
          fsAway."Wins" AS "AwayWins", fsAway."Losses" AS "AwayLosses",
          fsAway."ConferenceWins" AS "AwayConferenceWins", fsAway."ConferenceLosses" AS "AwayConferenceLosses",
          fsHome."Id" AS "HomeFranchiseSeasonId", fHome."DisplayName" AS "Home",
          fHome."Slug" AS "HomeSlug", fsrdHome."Current" AS "HomeRank",
          gsHome."Slug" AS "HomeConferenceSlug", gsHomeParent."Slug" AS "HomeParentConferenceSlug",
          fsHome."Wins" AS "HomeWins", fsHome."Losses" AS "HomeLosses",
          fsHome."ConferenceWins" AS "HomeConferenceWins", fsHome."ConferenceLosses" AS "HomeConferenceLosses",
          co."Details" AS "Spread", (co."Spread" * -1) AS "AwaySpread",
          co."Spread" AS "HomeSpread", co."OverUnder", co."OverOdds", co."UnderOdds"
        FROM public."Contest" c
        INNER JOIN public."SeasonPhase" sp ON sp."Id" = c."SeasonPhaseId"
        INNER JOIN public."SeasonWeek" sw ON sw."Id" = c."SeasonWeekId"
        INNER JOIN public."Venue" v ON v."Id" = c."VenueId"
        INNER JOIN public."Competition" comp ON comp."ContestId" = c."Id"
        INNER JOIN public."CompetitionStatus" cs ON cs."CompetitionId" = comp."Id"
        LEFT JOIN public."CompetitionNote" cn ON cn."CompetitionId" = comp."Id" AND cn."Type" = 'event'
        LEFT JOIN LATERAL (
          SELECT * FROM public."CompetitionOdds"
          WHERE "CompetitionId" = comp."Id" AND "ProviderId" IN ('{MatchupSqlBuilder.PreferredOddsProviderId}', '{MatchupSqlBuilder.FallbackOddsProviderId}')
          ORDER BY CASE WHEN "ProviderId" = '{MatchupSqlBuilder.PreferredOddsProviderId}' THEN 1 ELSE 2 END
          LIMIT 1
        ) co ON TRUE
        INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
        INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
        INNER JOIN public."GroupSeason" gsAway ON gsAway."Id" = fsAway."GroupSeasonId"
        LEFT JOIN public."GroupSeason" gsAwayParent ON gsAway."ParentId" = gsAwayParent."Id"
        INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
        INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
        INNER JOIN public."GroupSeason" gsHome ON gsHome."Id" = fsHome."GroupSeasonId"
        LEFT JOIN public."GroupSeason" gsHomeParent ON gsHome."ParentId" = gsHomeParent."Id"
        LEFT JOIN LATERAL (
          SELECT fsr.* FROM public."FranchiseSeasonRanking" fsr
          INNER JOIN public."SeasonWeek" rsw ON rsw."Id" = fsr."SeasonWeekId"
          WHERE fsr."FranchiseSeasonId" = fsAway."Id"
            AND fsr."DefaultRanking" = true AND fsr."Type" IN ('ap', 'cfp')
            AND rsw."StartDate" < c."StartDateUtc"
          ORDER BY rsw."StartDate" DESC LIMIT 1
        ) fsrAway ON TRUE
        LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdAway ON fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
        LEFT JOIN LATERAL (
          SELECT fsr.* FROM public."FranchiseSeasonRanking" fsr
          INNER JOIN public."SeasonWeek" rsw ON rsw."Id" = fsr."SeasonWeekId"
          WHERE fsr."FranchiseSeasonId" = fsHome."Id"
            AND fsr."DefaultRanking" = true AND fsr."Type" IN ('ap', 'cfp')
            AND rsw."StartDate" < c."StartDateUtc"
          ORDER BY rsw."StartDate" DESC LIMIT 1
        ) fsrHome ON TRUE
        LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdHome ON fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
        """;

    public GetMatchupForPreviewQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<MatchupForPreviewDto>> ExecuteAsync(
        GetMatchupForPreviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = $"{PreviewSql} WHERE c.\"Id\" = @ContestId";

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryFirstOrDefaultAsync<MatchupForPreviewDto>(
            new CommandDefinition(sql, new { query.ContestId }, cancellationToken: cancellationToken));

        if (result is null)
        {
            return new Failure<MatchupForPreviewDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("ContestId", $"Matchup preview for contest {query.ContestId} not found")]);
        }

        return new Success<MatchupForPreviewDto>(result);
    }

    public async Task<Result<Dictionary<Guid, MatchupForPreviewDto>>> ExecuteBatchAsync(
        GetMatchupsForPreviewBatchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.ContestIds.Length == 0)
        {
            return new Success<Dictionary<Guid, MatchupForPreviewDto>>(new Dictionary<Guid, MatchupForPreviewDto>());
        }

        var sql = $"{PreviewSql} WHERE c.\"Id\" = ANY(@ContestIds)";

        var connection = _dbContext.Database.GetDbConnection();
        var results = await connection.QueryAsync<MatchupForPreviewDto>(
            new CommandDefinition(sql, new { ContestIds = query.ContestIds }, cancellationToken: cancellationToken));

        var dict = results.ToDictionary(x => x.ContestId);
        return new Success<Dictionary<Guid, MatchupForPreviewDto>>(dict);
    }
}
