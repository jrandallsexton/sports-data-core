-- Recency bridge: each target-contest team's last @Count games of the
-- PRIOR season ("how did they finish?"), resolved by Franchise so the
-- season boundary is crossed correctly. "Side" says which target team the
-- row belongs to (Away/Home relative to the TARGET contest, not the
-- historical game). Same preview-safe semantics as head-to-head:
-- finalized + non-cancelled only, preseason excluded (TypeCode 1; NULL
-- phase kept), and inherently as-of (prior season only).
WITH target AS (
    SELECT
        c."Id",
        c."SeasonYear",
        fsAway."FranchiseId" AS "AwayFranchiseId",
        fsHome."FranchiseId" AS "HomeFranchiseId"
    FROM public."Contest" c
    INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
    INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
    WHERE c."Id" = @ContestId
),
sides AS (
    SELECT t."AwayFranchiseId" AS "FranchiseId", CAST('Away' AS text) AS "Side", t."SeasonYear" FROM target t
    UNION ALL
    SELECT t."HomeFranchiseId", 'Home', t."SeasonYear" FROM target t
)
SELECT s."Side", g.*
FROM sides s
CROSS JOIN LATERAL (
    SELECT
        c."StartDateUtc" AS "GameDate",
        c."SeasonYear",
        sp."Name" AS "Phase",
        c."EventNote" AS "Note",
        fHome."Name" AS "HomeTeam",
        fAway."Name" AS "AwayTeam",
        c."HomeScore",
        c."AwayScore",
        CASE
            WHEN c."WinnerFranchiseSeasonId" = c."HomeTeamFranchiseSeasonId" THEN fHome."Name"
            WHEN c."WinnerFranchiseSeasonId" = c."AwayTeamFranchiseSeasonId" THEN fAway."Name"
            ELSE NULL
        END AS "Winner",
        CASE
            WHEN c."SpreadWinnerFranchiseSeasonId" = c."HomeTeamFranchiseSeasonId" THEN fHome."Name"
            WHEN c."SpreadWinnerFranchiseSeasonId" = c."AwayTeamFranchiseSeasonId" THEN fAway."Name"
            ELSE NULL
        END AS "SpreadWinner",
        CASE c."OverUnder" WHEN 1 THEN 'Over' WHEN 2 THEN 'Under' ELSE NULL END AS "OverUnderResult"
    FROM public."Contest" c
    INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
    INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
    INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
    INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
    LEFT JOIN public."SeasonPhase" sp ON sp."Id" = c."SeasonPhaseId"
    WHERE (fsAway."FranchiseId" = s."FranchiseId" OR fsHome."FranchiseId" = s."FranchiseId")
      AND c."SeasonYear" = s."SeasonYear" - 1
      AND c."FinalizedUtc" IS NOT NULL
      AND c."CancelledUtc" IS NULL
      AND (sp."TypeCode" IS NULL OR sp."TypeCode" <> 1)
    ORDER BY c."StartDateUtc" DESC
    LIMIT @Count
) g
ORDER BY s."Side", g."GameDate" DESC
