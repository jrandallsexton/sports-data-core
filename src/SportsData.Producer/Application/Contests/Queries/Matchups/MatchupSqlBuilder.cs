using SportsData.Producer.Enums;

namespace SportsData.Producer.Application.Contests.Queries.Matchups;

/// <summary>
/// Shared SQL fragments for matchup queries.
/// The matchup queries share ~80% of their join structure — this avoids copy-paste.
/// </summary>
public static class MatchupSqlBuilder
{
    /// <summary>
    /// Preferred odds provider (ESPN Bet). Fallback is DraftKings.
    /// These map to SportsBook enum values stored in CompetitionOdds.ProviderId.
    /// </summary>
    public const int PreferredOddsProviderId = (int)SportsBook.EspnBet;       // 58
    public const int FallbackOddsProviderId = (int)SportsBook.DraftKings100;   // 100

    /// <summary>
    /// LATERAL join to select the best available odds provider (ESPN Bet preferred, DraftKings fallback).
    /// Expects Competition aliased as 'comp' to be in scope.
    /// </summary>
    public static readonly string OddsLateralJoin = $"""
        LEFT JOIN LATERAL (
          SELECT *
          FROM public."CompetitionOdds"
          WHERE "CompetitionId" = comp."Id"
            AND "ProviderId" IN ('{PreferredOddsProviderId}', '{FallbackOddsProviderId}')
          ORDER BY CASE WHEN "ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
          LIMIT 1
        ) co ON TRUE
        """;

    /// <summary>
    /// Standard matchup SELECT columns used by GetMatchupsForCurrentWeek,
    /// GetMatchupsForSeasonWeek, and GetMatchupByContestId.
    /// </summary>
    public const string MatchupSelectColumns = """
          sw."Id" AS "SeasonWeekId",
          s."Year" AS "SeasonYear",
          sw."Number" AS "SeasonWeek",
          c."Id" AS "ContestId",
          cn."Headline" AS "Headline",
          c."StartDateUtc" AS "StartDateUtc",
          cs."StatusTypeName" AS "Status",

          v."Name"                  AS "VenueName",
          v."City"                  AS "VenueCity",
          v."State"                 AS "VenueState",
          v."Latitude"              AS "VenueLatitude",
          v."Longitude"             AS "VenueLongitude",

          fAway."Slug"              AS "AwaySlug",
          fAway."ColorCodeHex"      AS "AwayColor",
          fAway."Abbreviation"      AS "AwayAbbreviation",
          fsrdAway."Current"        AS "AwayRank",
          fsAway."Wins"             AS "AwayWins",
          fsAway."Losses"           AS "AwayLosses",
          fsAway."ConferenceWins"   AS "AwayConferenceWins",
          fsAway."ConferenceLosses" AS "AwayConferenceLosses",
          gsAway."Slug"             AS "AwayConferenceSlug",
          fsAway."GroupSeasonMap"   AS "AwayGroupSeasonMap",

          fHome."Slug"              AS "HomeSlug",
          fHome."ColorCodeHex"      AS "HomeColor",
          fHome."Abbreviation"      AS "HomeAbbreviation",
          fsrdHome."Current"        AS "HomeRank",
          fsHome."Wins"             AS "HomeWins",
          fsHome."Losses"           AS "HomeLosses",
          fsHome."ConferenceWins"   AS "HomeConferenceWins",
          fsHome."ConferenceLosses" AS "HomeConferenceLosses",
          gsHome."Slug"             AS "HomeConferenceSlug",
          fsHome."GroupSeasonMap"   AS "HomeGroupSeasonMap",

          co."Details"        AS "Spread",
          (co."Spread" * -1)  AS "AwaySpread",
          co."Spread"         AS "HomeSpread",
          co."OverUnder"      AS "OverUnder",
          co."OverOdds"       AS "OverOdds",
          co."UnderOdds"      AS "UnderOdds"
        """;

    /// <summary>
    /// Standard matchup FROM/JOIN clause.
    /// Expects Contest aliased as 'c' to be already in scope (via FROM or JOIN).
    /// </summary>
    public static readonly string MatchupJoins = $"""
        INNER JOIN public."Competition" comp ON comp."ContestId" = c."Id"
        LEFT JOIN public."CompetitionNote" cn ON cn."CompetitionId" = comp."Id" AND cn."Type" = 'event'
        LEFT JOIN LATERAL (
          SELECT *
          FROM public."CompetitionOdds"
          WHERE "CompetitionId" = comp."Id"
            AND "ProviderId" IN ('{PreferredOddsProviderId}', '{FallbackOddsProviderId}')
          ORDER BY CASE WHEN "ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
          LIMIT 1
        ) co ON TRUE
        LEFT JOIN public."CompetitionStatus" cs ON cs."CompetitionId" = comp."Id"
        LEFT JOIN public."Venue" v ON v."Id" = c."VenueId"
        INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
        INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
        INNER JOIN public."GroupSeason" gsAway ON gsAway."Id" = fsAway."GroupSeasonId"
        INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
        INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
        INNER JOIN public."GroupSeason" gsHome ON gsHome."Id" = fsHome."GroupSeasonId"
        INNER JOIN public."SeasonWeek" sw ON sw."Id" = c."SeasonWeekId"
        INNER JOIN public."Season" s ON s."Id" = sw."SeasonId"
        """;

    /// <summary>
    /// Ranking joins that match by SeasonWeekId (used for current week/season week queries).
    /// </summary>
    public const string RankingJoinsBySeasonWeek = """
        LEFT JOIN public."FranchiseSeasonRanking" fsrAway ON fsrAway."FranchiseSeasonId" = fsAway."Id"
          AND fsrAway."DefaultRanking" = true AND fsrAway."Type" IN ('ap', 'cfp')
          AND fsrAway."SeasonWeekId" = sw."Id"
        LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdAway ON fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
        LEFT JOIN public."FranchiseSeasonRanking" fsrHome ON fsrHome."FranchiseSeasonId" = fsHome."Id"
          AND fsrHome."DefaultRanking" = true AND fsrHome."Type" IN ('ap', 'cfp')
          AND fsrHome."SeasonWeekId" = sw."Id"
        LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdHome ON fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
        """;

    /// <summary>
    /// Ranking joins that find the most recent ranking BEFORE the contest start date (used for preview/results).
    /// </summary>
    public const string RankingJoinsByContestDate = """
        LEFT JOIN LATERAL (
          SELECT fsr.*
          FROM public."FranchiseSeasonRanking" fsr
          INNER JOIN public."SeasonWeek" rsw ON rsw."Id" = fsr."SeasonWeekId"
          WHERE fsr."FranchiseSeasonId" = fsAway."Id"
            AND fsr."DefaultRanking" = true
            AND fsr."Type" IN ('ap', 'cfp')
            AND rsw."StartDate" <= c."StartDateUtc"
          ORDER BY rsw."StartDate" DESC
          LIMIT 1
        ) fsrAway ON TRUE
        LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdAway ON fsrdAway."FranchiseSeasonRankingId" = fsrAway."Id"
        LEFT JOIN LATERAL (
          SELECT fsr.*
          FROM public."FranchiseSeasonRanking" fsr
          INNER JOIN public."SeasonWeek" rsw ON rsw."Id" = fsr."SeasonWeekId"
          WHERE fsr."FranchiseSeasonId" = fsHome."Id"
            AND fsr."DefaultRanking" = true
            AND fsr."Type" IN ('ap', 'cfp')
            AND rsw."StartDate" <= c."StartDateUtc"
          ORDER BY rsw."StartDate" DESC
          LIMIT 1
        ) fsrHome ON TRUE
        LEFT JOIN public."FranchiseSeasonRankingDetail" fsrdHome ON fsrdHome."FranchiseSeasonRankingId" = fsrHome."Id"
        """;

    public const string MatchupOrderBy = """
        ORDER BY c."StartDateUtc", fHome."Slug"
        """;
}
