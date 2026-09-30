-- Companion to _debug_winner_consistency.sql: bucket the 5,258 hits.
-- The buckets mean DIFFERENT things:
--   wrong-side  = the Idaho bug (scores say one team, winner says the other)
--   null-winner = finalized but winner never derived (backfill-era gap?)
--   neither     = WinnerFranchiseSeasonId matches NEITHER contest column
--                 (dual-representation drift: competitor fsId vs contest cols)
SELECT
  EXTRACT(YEAR FROM c."StartDateUtc")::int AS "SeasonYear",
  CASE
    WHEN c."WinnerFranchiseSeasonId" IS NULL THEN 'null-winner'
    WHEN c."WinnerFranchiseSeasonId" NOT IN (c."AwayTeamFranchiseSeasonId", c."HomeTeamFranchiseSeasonId") THEN 'neither'
    ELSE 'wrong-side'
  END AS "Bucket",
  COUNT(*) AS "Contests"
FROM public."Contest" c
WHERE c."FinalizedUtc" IS NOT NULL
  AND c."AwayScore" IS NOT NULL
  AND c."HomeScore" IS NOT NULL
  AND c."AwayScore" <> c."HomeScore"
  AND (
        (c."AwayScore" > c."HomeScore" AND c."WinnerFranchiseSeasonId" IS DISTINCT FROM c."AwayTeamFranchiseSeasonId")
     OR (c."HomeScore" > c."AwayScore" AND c."WinnerFranchiseSeasonId" IS DISTINCT FROM c."HomeTeamFranchiseSeasonId")
  )
GROUP BY 1, 2
ORDER BY 1 DESC, 2;

-- SeasonYear	Bucket	Contests
-- 2026	null-winner	4
-- 2026	wrong-side	10
-- 2019	null-winner	265
-- 2018	null-winner	507
-- 2016	null-winner	113
-- 2015	null-winner	2177
-- 2014	null-winner	2181
