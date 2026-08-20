-- Margin fact for one franchise: the most recent game it WON by >= @Margin
-- (@Won = TRUE) or LOST by >= @Margin (@Won = FALSE), plus how often that
-- happened in the five seasons before the target, and the franchise's
-- earliest corpus season (the honest search floor). Score-margin tier: runs
-- on final scores only, no odds required.
-- Preview-safe by construction: finalized/non-cancelled, strictly before
-- @AsOf, preseason (SeasonPhase.TypeCode 1) excluded, NULL phase kept.
WITH franchise_games AS (
    SELECT
        c."StartDateUtc",
        c."SeasonYear",
        sp."Name" AS "Phase",
        c."EventNote" AS "Note",
        fHome."DisplayName" AS "HomeTeam",
        fAway."DisplayName" AS "AwayTeam",
        c."HomeScore",
        c."AwayScore",
        CASE
            WHEN c."WinnerFranchiseSeasonId" = c."HomeTeamFranchiseSeasonId" THEN fHome."DisplayName"
            WHEN c."WinnerFranchiseSeasonId" = c."AwayTeamFranchiseSeasonId" THEN fAway."DisplayName"
            ELSE NULL
        END AS "Winner",
        CASE
            WHEN c."SpreadWinnerFranchiseSeasonId" = c."HomeTeamFranchiseSeasonId" THEN fHome."DisplayName"
            WHEN c."SpreadWinnerFranchiseSeasonId" = c."AwayTeamFranchiseSeasonId" THEN fAway."DisplayName"
            ELSE NULL
        END AS "SpreadWinner",
        CASE WHEN fsHome."FranchiseId" = @FranchiseId
             THEN c."AwayTeamFranchiseSeasonId"
             ELSE c."HomeTeamFranchiseSeasonId"
        END AS "OpponentFranchiseSeasonId",
        CASE WHEN fsHome."FranchiseId" = @FranchiseId
             THEN c."HomeScore" - c."AwayScore"
             ELSE c."AwayScore" - c."HomeScore"
        END AS "OurMargin"
    FROM public."Contest" c
    INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
    INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
    INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
    INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
    LEFT JOIN public."SeasonPhase" sp ON sp."Id" = c."SeasonPhaseId"
    WHERE (fsHome."FranchiseId" = @FranchiseId OR fsAway."FranchiseId" = @FranchiseId)
      AND c."FinalizedUtc" IS NOT NULL
      AND c."CancelledUtc" IS NULL
      AND c."HomeScore" IS NOT NULL
      AND c."AwayScore" IS NOT NULL
      AND c."StartDateUtc" < @AsOf
      AND (sp."TypeCode" IS NULL OR sp."TypeCode" <> 1)
),
qualifying AS (
    SELECT *
    FROM franchise_games
    WHERE (@Won AND "OurMargin" >= @Margin)
       OR (NOT @Won AND "OurMargin" <= -@Margin)
)
-- Always exactly one row: "it has never happened" is the headline case and
-- must still carry the count (0) and the franchise's corpus floor.
SELECT
    q."StartDateUtc" AS "GameDate",
    q."SeasonYear",
    q."Phase",
    q."Note",
    q."HomeTeam",
    q."AwayTeam",
    q."HomeScore",
    q."AwayScore",
    q."Winner",
    q."SpreadWinner",
    q."OpponentFranchiseSeasonId",
    cnt."CountLastFiveSeasons",
    fl."SearchFloorSeason"
FROM (SELECT 1) AS one
LEFT JOIN LATERAL (
    SELECT * FROM qualifying ORDER BY "StartDateUtc" DESC LIMIT 1
) q ON TRUE
CROSS JOIN LATERAL (
    SELECT COUNT(*) AS "CountLastFiveSeasons"
    FROM qualifying WHERE "SeasonYear" >= @WindowStartSeason
) cnt
CROSS JOIN LATERAL (
    SELECT MIN("SeasonYear") AS "SearchFloorSeason" FROM franchise_games
) fl
