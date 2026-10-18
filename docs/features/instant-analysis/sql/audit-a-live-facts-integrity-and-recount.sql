-- Audit A: live instant-analysis facts after a slate (Database: sdProducer.FootballNfl or sdProducer.FootballNcaa).
-- Run against a restore taken after the games finalized. Read-only (temp tables only).
-- 1. Integrity checks: every count must be zero.
-- 2. Recount: recompute each game fact from the FINAL play data through the fact's play and list disagreements.
--    "Truth" here counts type 80 (NFL sack with an opponent fumble recovery) and 39 (fumble return touchdown),
--    which the classifier ignored as of 2026-10-12. A disagreement is either our bug or an ESPN change after
--    publish (reversal, re-credit, yardage fix); 2026-10-12 audit: ../2026-10-12-instant-analysis-nfl-first-slate.md.

\echo == 1. Integrity (all zero)
SELECT 'duplicate ordinal' AS check_name, count(*) FROM (
  SELECT "PlayId", "Ordinal" FROM "PlayAnalysis" WHERE "AnalysisRunId" IS NULL GROUP BY 1, 2 HAVING count(*) > 1) x
UNION ALL SELECT 'ordinal gap', count(*) FROM (
  SELECT "PlayId" FROM "PlayAnalysis" WHERE "AnalysisRunId" IS NULL GROUP BY 1
  HAVING min("Ordinal") <> 0 OR max("Ordinal") + 1 <> count(*)) x
UNION ALL SELECT 'orphan play', count(*) FROM "PlayAnalysis" pa
  WHERE NOT EXISTS (SELECT 1 FROM "CompetitionPlay" p WHERE p."Id" = pa."PlayId")
UNION ALL SELECT 'play in another competition', count(*) FROM "PlayAnalysis" pa
  JOIN "CompetitionPlay" p ON p."Id" = pa."PlayId" WHERE p."CompetitionId" <> pa."CompetitionId"
UNION ALL SELECT 'contest mismatch', count(*) FROM "PlayAnalysis" pa
  JOIN "Competition" c ON c."Id" = pa."CompetitionId" WHERE c."ContestId" <> pa."ContestId"
UNION ALL SELECT 'empty text', count(*) FROM "PlayAnalysis" WHERE coalesce(trim("Text"), '') = ''
UNION ALL SELECT 'live fact in a game not opted in', count(*) FROM "PlayAnalysis" pa
  WHERE pa."AnalysisRunId" IS NULL AND NOT EXISTS (
    SELECT 1 FROM "CompetitionStream" cs WHERE cs."CompetitionId" = pa."CompetitionId" AND cs."LiveAnalysisEnabled");

\echo == 1b. Coverage per opted-in game
SELECT cs."CompetitionId",
       (SELECT count(*) FROM "CompetitionPlay" p WHERE p."CompetitionId" = cs."CompetitionId") AS plays,
       (SELECT count(DISTINCT pa."PlayId") FROM "PlayAnalysis" pa
         WHERE pa."CompetitionId" = cs."CompetitionId" AND pa."AnalysisRunId" IS NULL) AS plays_with_facts,
       (SELECT count(*) FROM "PlayAnalysis" pa
         WHERE pa."CompetitionId" = cs."CompetitionId" AND pa."AnalysisRunId" IS NULL) AS facts
FROM "CompetitionStream" cs WHERE cs."LiveAnalysisEnabled" ORDER BY 2 DESC;

-- 2. Recount. ev = every counted event in the analysed games, by kind, with actors and victim (classifier roles).
DROP TABLE IF EXISTS ev;
CREATE TEMP TABLE ev AS
WITH p AS (
  SELECT p."Id" AS play_id, p."CompetitionId" AS comp, p."SequenceNumber"::bigint AS seq, p."TypeId" AS t,
         p."StartFranchiseSeasonId" AS off
  FROM "CompetitionPlay" p
  WHERE p."SequenceNumber" ~ '^[0-9]+$'
    AND p."CompetitionId" IN (SELECT DISTINCT "CompetitionId" FROM "PlayAnalysis" WHERE "AnalysisRunId" IS NULL)
), roles(kind, t, actor_role, victim_role) AS (VALUES
  ('Sack', '7', 'sackedBy', 'passer'),
  ('Sack', '80', 'sackedBy', 'passer'),
  ('Interception', '26', 'passDefender', 'passer'),
  ('Interception', '36', 'passDefender', 'passer'),
  ('Interception', '63', 'passDefender', 'passer'),
  ('FumbleLost', '29', 'recoverer', 'fumbler'),
  ('FumbleLost', '80', 'recoverer', 'fumbler'),
  ('FumbleLost', '39', 'recoverer', 'fumbler'),
  ('PassingTouchdown', '67', 'passer', NULL),
  ('ReceivingTouchdown', '67', 'receiver', NULL),
  ('RushingTouchdown', '68', 'rusher', NULL)
)
SELECT p.*, r.kind,
       array(SELECT DISTINCT pp."AthleteSeasonId" FROM "CompetitionPlayParticipant" pp
             WHERE pp."CompetitionPlayId" = p.play_id AND pp."Type" = r.actor_role AND pp."AthleteSeasonId" IS NOT NULL) AS actors,
       (SELECT pp."AthleteSeasonId" FROM "CompetitionPlayParticipant" pp
        WHERE pp."CompetitionPlayId" = p.play_id AND pp."Type" = r.victim_role AND pp."AthleteSeasonId" IS NOT NULL
        ORDER BY pp."Order" LIMIT 1) AS victim
FROM p JOIN roles r ON r.t = p.t;

DROP TABLE IF EXISTS f;
CREATE TEMP TABLE f AS
SELECT pa."Id", pa."PlayId", pa."CompetitionId" AS comp, pa."FactType" AS ft, pa."Facts" ->> 'kind' AS kind,
       (pa."Facts" ->> 'value')::numeric AS val,
       (pa."Facts" ->> 'athleteSeasonId')::uuid AS ath,
       (pa."Facts" -> 'provenance' ->> 'countedThroughSequence')::bigint AS seq,
       (pa."Facts" -> 'provenance' ->> 'previousLongest')::int AS prev_longest,
       pa."Text"
FROM "PlayAnalysis" pa WHERE pa."AnalysisRunId" IS NULL;

\echo == 2a. Count facts that disagree with the final play data
SELECT ft, kind, val AS stored, recount, left("Text", 80) AS fact, comp, seq
FROM (
  SELECT f.*, CASE ft
    WHEN 'GameCount' THEN (SELECT count(*) FROM ev
      WHERE ev.comp = f.comp AND ev.seq <= f.seq AND ev.kind = f.kind AND f.ath = ANY (ev.actors))
    WHEN 'GameCountAgainst' THEN (SELECT count(*) FROM ev
      WHERE ev.comp = f.comp AND ev.seq <= f.seq AND ev.kind = f.kind AND ev.victim = f.ath)
    WHEN 'TeamGameCount' THEN (SELECT count(DISTINCT ev.play_id) FROM ev
      JOIN "CompetitionPlay" me ON me."Id" = f."PlayId"
      WHERE ev.comp = f.comp AND ev.seq <= f.seq AND ev.off = me."StartFranchiseSeasonId"
        AND ((f.kind = 'Sack' AND ev.kind = 'Sack') OR (f.kind <> 'Sack' AND ev.kind IN ('Interception', 'FumbleLost'))))
    END AS recount
  FROM f WHERE ft IN ('GameCount', 'GameCountAgainst', 'TeamGameCount')) x
WHERE val <> recount
ORDER BY comp, seq, ft;

\echo == 2b. "Longest of the game" facts whose yardage or earlier best changed
SELECT left(f."Text", 80) AS fact, f.val AS stored_yards, me."StatYardage" AS final_yards,
       f.prev_longest AS stored_previous, recount_prev AS final_previous
FROM (
  SELECT f.*, (SELECT max(p."StatYardage") FROM "CompetitionPlay" p
     WHERE p."CompetitionId" = f.comp AND p."SequenceNumber" ~ '^[0-9]+$' AND p."SequenceNumber"::bigint < f.seq
       AND p."TypeId" = ANY (CASE WHEN f.kind = 'ExplosiveRun' THEN ARRAY['5', '68'] ELSE ARRAY['24', '67'] END)
       AND p."StatYardage" >= CASE WHEN f.kind = 'ExplosiveRun' THEN 10 ELSE 20 END) AS recount_prev
  FROM f WHERE ft = 'GameLongest') f
JOIN "CompetitionPlay" me ON me."Id" = f."PlayId"
WHERE f.val <> me."StatYardage" OR f.prev_longest IS DISTINCT FROM f.recount_prev OR me."StatYardage" <= f.recount_prev;

\echo == 2c. Facts on plays whose final type no longer produces that kind (ESPN reclassified the play)
SELECT f.ft, f.kind, left(f."Text", 70) AS fact, me."TypeId" AS final_type, left(me."Text", 100) AS final_play
FROM f JOIN "CompetitionPlay" me ON me."Id" = f."PlayId"
WHERE f.ft <> 'GameLongest' AND NOT EXISTS (SELECT 1 FROM ev WHERE ev.play_id = f."PlayId" AND ev.kind = f.kind);

\echo == 3. Low-value measures: share of "first"/"once" facts, and higher counts ranked out of the top two
SELECT "FactType", "Facts" ->> 'kind' AS kind, count(*) AS facts,
       count(*) FILTER (WHERE ("Facts" ->> 'value')::numeric = 1) AS value_1,
       count(*) FILTER (WHERE ("Facts" ->> 'value')::numeric >= 2) AS value_2_plus
FROM "PlayAnalysis" WHERE "AnalysisRunId" IS NULL AND "FactType" <> 'GameLongest'
GROUP BY 1, 2 ORDER BY 1, 3 DESC;

SELECT count(*) AS ranked_third_or_lower,
       count(*) FILTER (WHERE (h."Facts" ->> 'value')::numeric >= 2 AND EXISTS (
         SELECT 1 FROM "PlayAnalysis" s WHERE s."PlayId" = h."PlayId" AND s."AnalysisRunId" IS NULL AND s."Ordinal" < 2
           AND s."FactType" <> 'GameLongest' AND (s."Facts" ->> 'value')::numeric = 1)) AS count_2_plus_behind_a_first
FROM "PlayAnalysis" h WHERE h."AnalysisRunId" IS NULL AND h."Ordinal" >= 2;

SELECT p."PeriodNumber" AS quarter, count(*) AS longest_claims,
       count(*) FILTER (WHERE (pa."Facts" ->> 'value')::int - (pa."Facts" -> 'provenance' ->> 'previousLongest')::int <= 2) AS margin_2_or_less
FROM "PlayAnalysis" pa JOIN "CompetitionPlay" p ON p."Id" = pa."PlayId"
WHERE pa."AnalysisRunId" IS NULL AND pa."FactType" = 'GameLongest'
GROUP BY 1 ORDER BY 1;
