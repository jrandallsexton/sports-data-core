-- Remediation: SeasonPollWeek.SeasonWeekId misfiled, 2026 season.
--
-- The processor resolved the week from ESPN's current-week pointer (+1)
-- instead of the poll's own week, so rows landed one slot late (or NULL when
-- the pointer was absent). The fixed processor files by the poll's own
-- phase + week. Every row's ExternalId SourceUrl names both:
--
--     .../seasons/2026/types/{phaseTypeCode}/weeks/{weekNumber}/rankings/{pollId}
--
-- so the URL is the key here, not OccurrenceNumber (which repeats: a
-- preseason poll and a week-1 poll are both occurrence 1).
--
-- Run AFTER the fixed Producer is deployed. One transaction: step 2 NULLs
-- the affected rows so IX_SeasonPollWeek_SeasonPollId_SeasonWeekId is free
-- before step 3 re-points. Ends in ROLLBACK; change to COMMIT once the
-- preview and verify look right.

BEGIN;

DROP TABLE IF EXISTS _spw_move;

CREATE TEMP TABLE _spw_move AS
SELECT
    spw."Id"                                                   AS "SeasonPollWeekId",
    p."ShortName"                                              AS "Poll",
    spw."OccurrenceDisplay"                                    AS "Display",
    (regexp_match(x."SourceUrl", '/types/(\d+)/weeks/(\d+)/'))[1]::int AS "UrlPhase",
    (regexp_match(x."SourceUrl", '/types/(\d+)/weeks/(\d+)/'))[2]::int AS "UrlWeek",
    spw."SeasonWeekId"                                         AS "FromSeasonWeekId",
    sw_from."Number"                                           AS "FromWeek",
    ph_from."TypeCode"                                         AS "FromPhase"
FROM public."SeasonPollWeek" spw
JOIN public."SeasonPoll"              p       ON p."Id"  = spw."SeasonPollId"
JOIN public."SeasonPollWeekExternalId" x      ON x."SeasonPollWeekId" = spw."Id"
LEFT JOIN public."SeasonWeek"         sw_from ON sw_from."Id" = spw."SeasonWeekId"
LEFT JOIN public."SeasonPhase"        ph_from ON ph_from."Id" = sw_from."SeasonPhaseId"
WHERE p."SeasonYear" = 2026;

ALTER TABLE _spw_move ADD COLUMN "ToSeasonWeekId" uuid;

UPDATE _spw_move m
SET "ToSeasonWeekId" = sw."Id"
FROM public."SeasonWeek" sw
JOIN public."SeasonPhase" ph ON ph."Id" = sw."SeasonPhaseId"
JOIN public."Season"      s  ON s."Id"  = ph."SeasonId"
WHERE s."Year" = 2026
  AND ph."TypeCode" = m."UrlPhase"
  AND sw."Number"   = m."UrlWeek";

-- 1) Preview. Every row should have a ToSeasonWeekId; "Moves" marks the ones
--    that change. Stop (ROLLBACK) if any ToSeasonWeekId is NULL.
SELECT "Poll", "Display", "UrlPhase", "UrlWeek", "FromPhase", "FromWeek",
       "ToSeasonWeekId" IS NOT NULL                              AS "Resolved",
       "ToSeasonWeekId" IS DISTINCT FROM "FromSeasonWeekId"      AS "Moves"
FROM _spw_move
ORDER BY "Poll", "UrlPhase", "UrlWeek";

-- 2) Free the slots (NULLs never collide on the unique index).
UPDATE public."SeasonPollWeek" spw
SET "SeasonWeekId" = NULL,
    "ModifiedUtc"  = now(),
    "ModifiedBy"   = '00000000-0000-0000-0000-000000000000'
FROM _spw_move m
WHERE spw."Id" = m."SeasonPollWeekId"
  AND m."ToSeasonWeekId" IS NOT NULL
  AND m."ToSeasonWeekId" IS DISTINCT FROM m."FromSeasonWeekId";

-- 3) Re-point each row at the week its own URL names.
UPDATE public."SeasonPollWeek" spw
SET "SeasonWeekId" = m."ToSeasonWeekId"
FROM _spw_move m
WHERE spw."Id" = m."SeasonPollWeekId"
  AND m."ToSeasonWeekId" IS NOT NULL
  AND spw."SeasonWeekId" IS NULL;

-- 4) Verify: expect 0.
SELECT count(*) AS "StillMisfiled"
FROM public."SeasonPollWeek" spw
JOIN _spw_move m ON m."SeasonPollWeekId" = spw."Id"
WHERE m."ToSeasonWeekId" IS NOT NULL
  AND spw."SeasonWeekId" IS DISTINCT FROM m."ToSeasonWeekId";

ROLLBACK; --COMMIT;   -- change to COMMIT once Resolved is all true and StillMisfiled = 0
