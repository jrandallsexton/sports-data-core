-- API database (sdApi.*). Run AFTER the CaptureScoresAndOverUnder migration
-- has applied (the API applies it at startup).
--
-- Why: MatchupPreviewProcessor stored the over/under pick as
-- `== 1 ? Over : Under`, so the model's 0 (no line, prompt rule 18) and a
-- missing value were both saved as Under (2), and None (0) was never stored.
-- Fixed in code (PR: capture scores and over/under); this corrects the rows
-- already written. The model's actual answer is read from the linked
-- MatchupPreviewPrompt capture's RawResponse. Previews with no capture
-- (older than the capture table) have no record of the answer and are left
-- unchanged.
--
-- Local copy, 2026-09-29: 346 said 0 + 10 absent = 356 rows to correct;
-- 125 Over and 391 Under were already right; 1304 previews have no capture.

BEGIN;

-- ── A. Preview rows stored as Under where the model did not say Under ──────
WITH said AS (
    SELECT mp."Id",
           substring(c."RawResponse" from '"overUnderPrediction"\s*:\s*"?(-?[0-9]+)') AS raw
    FROM public."MatchupPreview" mp
    JOIN public."MatchupPreviewPrompt" c ON c."MatchupPreviewId" = mp."Id"
    WHERE mp."OverUnderPrediction" = 2
)
UPDATE public."MatchupPreview" mp
SET "OverUnderPrediction" = 0   -- None
FROM said
WHERE mp."Id" = said."Id"
  AND (said.raw IS NULL OR said.raw NOT IN ('1', '2'));

-- ── B. (Optional) Backfill the new capture columns for existing captures ───
-- Same parsing as the code: scores as given; over/under 0 None, 1 Over,
-- 2 Under, anything else or absent stays NULL. Only rows still NULL, so it
-- is safe to re-run. Delete this block to skip the backfill.
UPDATE public."MatchupPreviewPrompt" c
SET "AwayScore" = substring(c."RawResponse" from '"awayScore"\s*:\s*"?(-?[0-9]+)')::int,
    "HomeScore" = substring(c."RawResponse" from '"homeScore"\s*:\s*"?(-?[0-9]+)')::int,
    "OverUnderPrediction" = CASE substring(c."RawResponse" from '"overUnderPrediction"\s*:\s*"?(-?[0-9]+)')
                                WHEN '0' THEN 0 WHEN '1' THEN 1 WHEN '2' THEN 2 END
WHERE c."RawResponse" IS NOT NULL
  AND c."AwayScore" IS NULL AND c."HomeScore" IS NULL AND c."OverUnderPrediction" IS NULL;

-- ── Verify: stored value vs what the model said (expect no 0/absent -> 2) ──
SELECT coalesce(substring(c."RawResponse" from '"overUnderPrediction"\s*:\s*"?(-?[0-9]+)'), '(absent)') AS "ModelSaid",
       mp."OverUnderPrediction" AS "Stored",
       count(*) AS "Previews"
FROM public."MatchupPreview" mp
JOIN public."MatchupPreviewPrompt" c ON c."MatchupPreviewId" = mp."Id"
GROUP BY 1, 2
ORDER BY 1, 2;

-- Preview and capture now agree wherever both exist (expect 0).
SELECT count(*) AS "PreviewCaptureMismatches"
FROM public."MatchupPreview" mp
JOIN public."MatchupPreviewPrompt" c ON c."MatchupPreviewId" = mp."Id"
WHERE c."OverUnderPrediction" IS NOT NULL
  AND c."OverUnderPrediction" <> mp."OverUnderPrediction";

COMMIT;
