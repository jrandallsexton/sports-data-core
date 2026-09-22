-- Remediation: stamp PredictedStraightUpWinnerId / PredictedSpreadWinnerId on
-- every production (Generate-mode) MatchupPreviewPrompt capture from the
-- MatchupPreview it produced.
--
-- DATABASE: sdApi.All. Run once.
--
-- RUN AFTER the processor fix is deployed (fix/capture-predicted-winners-
-- generate-mode). Before that, every new generation adds another row this
-- script would have to catch.
--
-- Context (2026-09-22): only the Experiment path of MatchupPreviewProcessor
-- ever wrote the two columns onto the capture; the Generate path stamped the
-- preview id, model, and raw response and stopped. The parsed picks went onto
-- the MatchupPreview row instead. So all 631 Generate captures (since the
-- first on 2026-08-07) carry NULLs while the raw response and the preview
-- both hold the ids. StatBot was never affected: it reads the preview. The
-- Model Lab matrix WAS: it scores captures keyed by ModelId, and every
-- production capture carries the production model's id, so that column read
-- "no pick" on every contest.
--
-- Scope: Generate captures with a linked preview and a NULL straight-up id.
-- Copies BOTH columns from the preview as they are. A NULL spread winner on
-- the preview stays NULL: 336 of those are games with no line (pick'em or
-- MLB), which the validator forbids picking against.
--
-- NOT touched: the 5 Generate captures with a raw response and no preview.
-- Those failed validation twice; the picks in their raw JSON are invalid by
-- the validator's own ruling and are worth nothing to the matrix. Experiment
-- captures are not touched either; their 15 NULLs are parse failures.
--
-- Ends in ROLLBACK. Change the last line to COMMIT once the preview matches.

BEGIN;

DROP TABLE IF EXISTS _capture_fill;
CREATE TEMP TABLE _capture_fill AS
SELECT c."Id"                       AS capture_id,
       c."ContestId",
       c."CreatedUtc",
       p."PredictedStraightUpWinner" AS su,
       p."PredictedSpreadWinner"     AS ats
FROM public."MatchupPreviewPrompt" c
JOIN public."MatchupPreview" p ON p."Id" = c."MatchupPreviewId"
WHERE c."Mode" = 0                                  -- PreviewGenerationMode.Generate
  AND c."PredictedStraightUpWinnerId" IS NULL
  AND p."PredictedStraightUpWinner" IS NOT NULL;

-- Preview: expect ~626 to fill (as of 2026-09-22 local copy), ~340 of them
-- with a NULL ats, and 5 left alone.
SELECT (SELECT count(*) FROM _capture_fill)                              AS will_fill,
       (SELECT count(*) FROM _capture_fill WHERE ats IS NULL)            AS will_fill_ats_null,
       (SELECT count(*) FROM public."MatchupPreviewPrompt"
         WHERE "Mode" = 0 AND "MatchupPreviewId" IS NULL
           AND "PredictedStraightUpWinnerId" IS NULL)                    AS left_alone_no_preview,
       (SELECT min("CreatedUtc") FROM _capture_fill)                     AS earliest,
       (SELECT max("CreatedUtc") FROM _capture_fill)                     AS latest;

UPDATE public."MatchupPreviewPrompt" c
SET "PredictedStraightUpWinnerId" = f.su,
    "PredictedSpreadWinnerId"     = f.ats
FROM _capture_fill f
WHERE c."Id" = f.capture_id;

-- Verify: no Generate capture with a preview is left without a straight-up id,
-- and the two columns agree with the preview row for one.
SELECT count(*) AS remaining_missing_with_preview
FROM public."MatchupPreviewPrompt" c
WHERE c."Mode" = 0 AND c."MatchupPreviewId" IS NOT NULL
  AND c."PredictedStraightUpWinnerId" IS NULL;

SELECT c."ContestId",
       c."PredictedStraightUpWinnerId" = p."PredictedStraightUpWinner" AS su_matches,
       c."PredictedSpreadWinnerId" IS NOT DISTINCT FROM p."PredictedSpreadWinner" AS ats_matches
FROM public."MatchupPreviewPrompt" c
JOIN public."MatchupPreview" p ON p."Id" = c."MatchupPreviewId"
WHERE c."ContestId" = '4dde6412-6e35-a7a1-8055-97e897047d88';

ROLLBACK;
