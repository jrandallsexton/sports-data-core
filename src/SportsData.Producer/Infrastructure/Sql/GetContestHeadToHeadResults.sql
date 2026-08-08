-- Last @Count head-to-head meetings between the target contest's two
-- FRANCHISES (cross-season identity via Franchise, since FranchiseSeasonIds
-- change yearly). Preview-safe by construction:
--   * finalized, non-cancelled games only
--   * as-of: strictly before the target's start (the target contest can
--     never leak into its own history — see the answer-leak finding in
--     docs/metrics-modeling/matchup-preview-data-inputs.md)
--   * preseason excluded (SeasonPhase.TypeCode 1) — system-testing data
--     only, per policy 2026-08-08. NULL phase (old rows) is kept.
WITH target AS (
    SELECT
        c."Id",
        c."StartDateUtc",
        fsAway."FranchiseId" AS "AwayFranchiseId",
        fsHome."FranchiseId" AS "HomeFranchiseId"
    FROM public."Contest" c
    INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
    INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
    WHERE c."Id" = @ContestId
)
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
FROM target t
INNER JOIN public."Contest" c ON c."Id" <> t."Id"
INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
LEFT JOIN public."SeasonPhase" sp ON sp."Id" = c."SeasonPhaseId"
WHERE ((fHome."Id" = t."HomeFranchiseId" AND fAway."Id" = t."AwayFranchiseId")
    OR (fHome."Id" = t."AwayFranchiseId" AND fAway."Id" = t."HomeFranchiseId"))
  AND c."FinalizedUtc" IS NOT NULL
  AND c."CancelledUtc" IS NULL
  AND c."StartDateUtc" < t."StartDateUtc"
  AND (sp."TypeCode" IS NULL OR sp."TypeCode" <> 1)
ORDER BY c."StartDateUtc" DESC
LIMIT @Count
