-- Task 08: post-game audit. Walks a game play by play with its instant-analysis facts, including plays with none.
-- Database: sdProducer.<Sport> (Producer). Set the competition id below; AnalysisRunId IS NULL = live facts
-- (replace with = '<run id>' to audit a lab replay run).

-- A. Every play in order, with its facts (LEFT JOIN keeps plays that produced nothing).
SELECT p."SequenceNumber", p."TypeId", left(p."Text", 80) AS play,
       t."Ordinal", t."FactType", t."Text" AS fact,
       t."Facts" -> 'provenance' ->> 'seasonValue' AS season_value,
       t."Facts" -> 'provenance' ->> 'gameValue' AS game_value,
       t."Facts" -> 'provenance' ->> 'writtenAfterKickoff' AS doc_after_kickoff
FROM "CompetitionPlay" p
LEFT JOIN "PlayAnalysis" t ON t."PlayId" = p."Id" AND t."AnalysisRunId" IS NULL
WHERE p."CompetitionId" = '6f3ebe93-0d6e-144e-6aec-8e6bf42180d4'
ORDER BY p."SequenceNumber"::bigint, t."Priority", t."Ordinal";

-- B. Summary: plays, plays with facts, facts by type, and any duplicate sets (must be zero).
SELECT count(DISTINCT p."Id") AS plays,
       count(DISTINCT t."PlayId") AS plays_with_facts,
       count(t."Id") AS facts
FROM "CompetitionPlay" p
LEFT JOIN "PlayAnalysis" t ON t."PlayId" = p."Id" AND t."AnalysisRunId" IS NULL
WHERE p."CompetitionId" = '6f3ebe93-0d6e-144e-6aec-8e6bf42180d4';

SELECT "FactType", count(*) FROM "PlayAnalysis"
WHERE "CompetitionId" = '6f3ebe93-0d6e-144e-6aec-8e6bf42180d4' AND "AnalysisRunId" IS NULL
GROUP BY 1 ORDER BY 2 DESC;

SELECT "PlayId", "Ordinal", count(*) FROM "PlayAnalysis"
WHERE "CompetitionId" = '6f3ebe93-0d6e-144e-6aec-8e6bf42180d4'
GROUP BY "PlayId", "AnalysisRunId", "Ordinal" HAVING count(*) > 1;
