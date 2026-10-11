-- Audit: how often "longest run of the game" fires across 2026 NCAA games (Database: sdProducer.FootballNcaa).
-- fires = original rule (>= 10 yards and longer than every earlier run); fires_if_beats_explosive = must beat an earlier
-- 10+ yard run, which became the rule on 2026-10-11 (about 1.65 per game vs 2.65 across 704 games).
WITH runs AS (
  SELECT p."CompetitionId", p."SequenceNumber"::bigint seq, p."StatYardage" y,
         max(p."StatYardage") OVER (PARTITION BY p."CompetitionId" ORDER BY p."SequenceNumber"::bigint
                                    ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) prev
  FROM "CompetitionPlay" p
  JOIN "Competition" c ON c."Id" = p."CompetitionId"
  JOIN "Contest" k ON k."Id" = c."ContestId"
  WHERE p."TypeId" IN ('5','68') AND p."SequenceNumber" ~ '^[0-9]+$' AND k."SeasonYear" = 2026
), claims AS (
  SELECT *, (y >= 10 AND (prev IS NULL OR y > prev)) AS fires,
            (y >= 10 AND prev >= 10 AND y > prev) AS fires_if_beats_explosive
  FROM runs
), per_game AS (
  SELECT "CompetitionId", count(*) FILTER (WHERE fires) f, count(*) FILTER (WHERE fires_if_beats_explosive) g,
         count(*) FILTER (WHERE y >= 10) explosive
  FROM claims GROUP BY 1
)
SELECT count(*) games, round(avg(explosive),1) explosive_runs_per_game,
       round(avg(f),2) longest_claims_per_game, max(f) max_claims,
       round(avg(g),2) claims_if_must_beat_explosive,
       percentile_cont(0.9) WITHIN GROUP (ORDER BY f) p90
FROM per_game;
