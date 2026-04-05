using Dapper;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

using System.Collections.Generic;
using System.Linq;

namespace SportsData.Producer.Application.Franchises.Queries.GetTeamCard;

public interface IGetTeamCardQueryHandler
{
    Task<Result<TeamCardDto>> ExecuteAsync(
        GetTeamCardQuery query,
        CancellationToken cancellationToken = default);
}

public class GetTeamCardQueryHandler : IGetTeamCardQueryHandler
{
    private readonly TeamSportDataContext _dbContext;

    public GetTeamCardQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    private const string CardSql = """
        WITH next_week AS (
          SELECT sw."Id" AS "SeasonWeekId",
                 sw."Number" AS "WeekNumber",
                 s."Id" AS "SeasonId",
                 s."Year" AS "SeasonYear"
          FROM public."Season" s
          JOIN public."SeasonWeek" sw ON sw."SeasonId" = s."Id"
          JOIN public."SeasonPhase" sp ON sp."Id" = sw."SeasonPhaseId"
          WHERE sp."Name" = 'Regular Season'
            AND sw."StartDate" <= CURRENT_DATE and sw."EndDate" > CURRENT_DATE
          ORDER BY sw."StartDate"
          LIMIT 1
        )
        SELECT DISTINCT ON (F."Id")
            FS."Id" AS "FranchiseSeasonId",
            F."Slug" AS "Slug",
            F."DisplayName" AS "Name",
            F."DisplayNameShort" AS "ShortName",
            fsrd."Current" AS "Ranking",
            GS."Name" AS "ConferenceName",
            GS."ShortName" AS "ConferenceShortName",
            GS."Slug" AS "ConferenceSlug",
            FS."Wins" || '-' || FS."Losses" || '-' || FS."Ties" AS "OverallRecord",
            FS."ConferenceWins" || '-' || FS."ConferenceLosses" || '-' || FS."ConferenceTies" AS "ConferenceRecord",
            F."ColorCodeHex" AS "ColorPrimary",
            F."ColorCodeAltHex" AS "ColorSecondary",
            FL."Uri" AS "LogoUrl",
            NULL AS "HelmetUrl",
            F."Location" AS "Location",
            V."Name" AS "StadiumName",
            V."Capacity" AS "StadiumCapacity"
        FROM
            PUBLIC."Franchise" F
            INNER JOIN PUBLIC."FranchiseSeason" FS on FS."FranchiseId" = F."Id"
            LEFT JOIN public."FranchiseSeasonRanking" fsr on fsr."FranchiseSeasonId" = FS."Id" and
                fsr."DefaultRanking" = true and fsr."Type" in ('ap', 'cfp') and
                fsr."SeasonWeekId" = (select "SeasonWeekId" from next_week)
            LEFT JOIN public."FranchiseSeasonRankingDetail" fsrd on fsrd."FranchiseSeasonRankingId" = fsr."Id"
            INNER JOIN PUBLIC."GroupSeason" GS ON GS."Id" = FS."GroupSeasonId"
            LEFT JOIN PUBLIC."FranchiseLogo" FL ON FL."FranchiseId" = F."Id"
            LEFT JOIN PUBLIC."Venue" V ON V."Id" = F."VenueId"
        WHERE
            F."Slug" = @Slug and FS."SeasonYear" = @SeasonYear
        ORDER BY
            F."Id",
            FL."CreatedUtc" ASC NULLS LAST
        """;

    private const string SeasonsSql = """
        SELECT fs."SeasonYear"
        FROM public."FranchiseSeason" fs
        INNER JOIN public."Franchise" f ON f."Id" = fs."FranchiseId"
        WHERE f."Slug" = @Slug
        ORDER BY fs."SeasonYear" DESC
        """;

    private const string ScheduleSql = """
        SELECT
            C."Id" AS "ContestId",
            sw."Number" AS "Week",
            C."StartDateUtc" AS "Date",
            CASE
                WHEN fAway."Slug" = @Slug THEN fHome."DisplayName"
                ELSE fAway."DisplayName"
            END AS "Opponent",
            CASE
                WHEN fAway."Slug" = @Slug THEN fHome."DisplayNameShort"
                ELSE fAway."DisplayNameShort"
            END AS "OpponentShortName",
            CASE
                WHEN fAway."Slug" = @Slug THEN fHome."Slug"
                ELSE fAway."Slug"
            END AS "OpponentSlug",
            V."Name" || ' [' || V."City" || ', ' || V."State" || ']' as "Location",
            CASE
                WHEN fAway."Slug" = @Slug THEN 'Away'
                ELSE 'Home'
            END AS "LocationType",
            cs."StatusDescription" as "Status",
            c."FinalizedUtc" as "FinalizedUtc",
            c."AwayScore" as "AwayScore",
            c."HomeScore" as "HomeScore",
            CASE
                WHEN fAway."Slug" = @Slug AND c."WinnerFranchiseId" = fsAway."Id" THEN true
                WHEN fHome."Slug" = @Slug AND c."WinnerFranchiseId" = fsHome."Id" THEN true
                ELSE null
            END AS "WasWinner"
        FROM public."Contest" C
        INNER JOIN public."Competition" COMP on COMP."ContestId" = C."Id"
        INNER JOIN public."CompetitionStatus" CS on CS."CompetitionId" = COMP."Id"
        INNER JOIN public."SeasonWeek" SW on SW."Id" = C."SeasonWeekId"
        INNER JOIN public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
        INNER JOIN public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
        INNER JOIN public."Franchise" fAway on fAway."Id" = fsAway."FranchiseId"
        INNER JOIN public."Franchise" fHome on fHome."Id" = fsHome."FranchiseId"
        LEFT JOIN public."Venue" v on v."Id" = c."VenueId"
        WHERE (fAway."Slug" = @Slug OR fHome."Slug" = @Slug) AND C."SeasonYear" = @SeasonYear
        ORDER BY C."StartDateUtc"
        """;

    public async Task<Result<TeamCardDto>> ExecuteAsync(
        GetTeamCardQuery query,
        CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var parameters = new { query.Slug, query.SeasonYear };

        var teamCard = await connection.QueryFirstOrDefaultAsync<TeamCardDto>(
            new CommandDefinition(CardSql, parameters, cancellationToken: cancellationToken));

        if (teamCard is null)
        {
            return new Failure<TeamCardDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("TeamCard", $"Team card not found for slug '{query.Slug}' season {query.SeasonYear}")]);
        }

        var seasons = (await connection.QueryAsync<int>(
            new CommandDefinition(SeasonsSql, new { query.Slug }, cancellationToken: cancellationToken))).ToList();

        var schedule = (await connection.QueryAsync<TeamCardScheduleItemDto>(
            new CommandDefinition(ScheduleSql, parameters, cancellationToken: cancellationToken))).ToList();

        var result = teamCard with
        {
            SeasonYears = seasons,
            Schedule = schedule
        };

        return new Success<TeamCardDto>(result);
    }
}
