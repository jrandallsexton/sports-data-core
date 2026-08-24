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
    WHERE c."Id" = 'ff20c524-d45e-6c6c-d7a9-3082020d351b'
)
SELECT
    c."StartDateUtc" AS "GameDate",
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
    co."Details" AS "SpreadCurrentDetails", co."Spread" AS "SpreadCurrent",
    cto."SpreadPointsOpen" AS "SpreadOpen",
    co."OverUnder" AS "OverUnderCurrent", co."TotalPointsOpen" AS "OverUnderOpen",
    co."OverOdds", co."UnderOdds",
    co."ProviderName" AS "ProviderName",
    CASE c."OverUnder" WHEN 1 THEN 'Over' WHEN 2 THEN 'Under' ELSE NULL END AS "OverUnderResult"
FROM target t
INNER JOIN public."Contest" c ON c."Id" <> t."Id"
INNER JOIN public."Competition" comp on comp."ContestId" = c."Id"
LEFT JOIN LATERAL (
  SELECT * FROM public."CompetitionOdds"
  WHERE "CompetitionId" = comp."Id" AND "ProviderId" IN ('58', '100')
  ORDER BY CASE WHEN "ProviderId" = '58' THEN 1 ELSE 2 END
  LIMIT 1
) co ON TRUE
LEFT JOIN public."CompetitionTeamOdds" cto ON cto."CompetitionOddsId" = co."Id" AND cto."Side" = 'Home'
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
LIMIT 10

-- GameDate	SeasonYear	Phase	Note	HomeTeam	AwayTeam	HomeScore	AwayScore	Winner	SpreadWinner	SpreadCurrentDetails	SpreadCurrent	SpreadOpen	OverUnderCurrent	OverUnderOpen	OverOdds	UnderOdds	ProviderName	OverUnderResult
-- 2025-09-14 20:05:00+00	2025	Regular Season	NULL	Arizona Cardinals	Carolina Panthers	27	22	Arizona Cardinals	Carolina Panthers	ARI -6.5	-6.500000	-6.500000	45.500000	45.500000	-105.000000	-115.000000	ESPN BET	Over
-- 2024-12-22 18:00:00+00	2024	Regular Season	NULL	Carolina Panthers	Arizona Cardinals	36	30	Carolina Panthers	Carolina Panthers	ARI -5.5	5.500000	4.500000	47.500000	46.500000	100.000000	-120.000000	ESPN BET	Over
-- 2022-10-02 20:05:00+00	2022	Regular Season	NULL	Carolina Panthers	Arizona Cardinals	16	26	Arizona Cardinals	Arizona Cardinals	CAR -0.5	-0.500000	NULL	44.500000	NULL	-115.000000	-115.000000	ESPN BET	Under
-- 2021-11-14 21:05:00+00	2021	Regular Season	NULL	Arizona Cardinals	Carolina Panthers	10	34	Carolina Panthers	Carolina Panthers	NULL	NULL	NULL	NULL	NULL	NULL	NULL	NULL	Over
-- 2020-10-04 17:00:00+00	2020	Regular Season	NULL	Carolina Panthers	Arizona Cardinals	31	21	Carolina Panthers	NULL	NULL	NULL	NULL	NULL	NULL	NULL	NULL	NULL	NULL
-- 2019-09-22 20:05:00+00	2019	Regular Season	NULL	Arizona Cardinals	Carolina Panthers	20	38	Carolina Panthers	Carolina Panthers	NULL	NULL	NULL	NULL	NULL	NULL	NULL	NULL	Over
-- 2016-10-30 17:00:00+00	2016	Regular Season	NULL	Carolina Panthers	Arizona Cardinals	30	20	Carolina Panthers	Carolina Panthers	NULL	NULL	NULL	NULL	NULL	NULL	NULL	NULL	Over
-- 2016-01-24 23:40:00+00	2015	Postseason	NULL	Carolina Panthers	Arizona Cardinals	49	15	Carolina Panthers	Carolina Panthers	NULL	NULL	NULL	NULL	NULL	NULL	NULL	NULL	Over
-- 2015-01-03 21:20:00+00	2014	Postseason	NULL	Carolina Panthers	Arizona Cardinals	27	16	Carolina Panthers	Carolina Panthers	NULL	NULL	NULL	NULL	NULL	NULL	NULL	NULL	Over
-- 2013-10-06 20:05:00+00	2013	Regular Season	NULL	Arizona Cardinals	Carolina Panthers	22	6	Arizona Cardinals	Arizona Cardinals	NULL	NULL	NULL	NULL	NULL	NULL	NULL	NULL	Under
