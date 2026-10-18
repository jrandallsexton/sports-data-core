-- Model Consensus Lab: replace the seventh seat (2026-10-14).
-- OpenRouter retired qwen/qwen3-max on 2026-10-09 (every call now 404s:
-- "Qwen3 Max was deprecated on Oct 9, 2026. We recommend migrating to
-- Qwen3.8 Max (0902)"). Its replacement goes in as a NEW row and the old
-- row is deactivated, not repointed: lab results already stamped with the
-- Qwen 3 Max row stay attributed to the weights that produced them.
--
-- Id, pricing and release date from OpenRouter's public catalog
-- (GET https://openrouter.ai/api/v1/models, read 2026-10-14):
-- qwen/qwen3.8-max-0902, listed 2026-09-03, $2.00 in / $6.00 out per MTok.
-- KnowledgeCutoffUtc is NULL: Alibaba publishes none for 3.8 Max, and the
-- lab treats NULL as higher-risk by design. A September 2026 release most
-- likely trained into 2026, so its 2025-season backtests are suspect where
-- Qwen 3 Max's (Jun 30 2025 cutoff) were clean.
--
-- Target DB: API (AppDataContext). Re-runnable: the insert skips on any
-- unique clash, the update is a no-op once applied.

SELECT "Id", "Name", "ApiModelId", "Gateway", "IsActive", "IsDefault"
FROM "Model" ORDER BY "Name";

SELECT "Id", "Name", "IsActive" FROM "ModelProvider"
WHERE "Id" = 'a0000000-0000-0000-0000-000000000006';   -- Alibaba

SELECT max("MigrationId") FROM "__EFMigrationsHistory";  -- expect 20261001112525_UserPickBetPoints

SELECT max("MigrationId") FROM "__EFMigrationsHistory";  -- 20261015093833_AddModelFamily

SELECT f."Name" AS family, m."Name", m."Gateway", m."IsActive"
FROM "Model" m JOIN "ModelFamily" f ON f."Id" = m."ModelFamilyId"
ORDER BY 1, 2;

-- Must return NO rows (IX_Model_SingleActiveLabSeat also guarantees it):
SELECT "ModelFamilyId", count(*) FROM "Model"
WHERE "IsActive" AND "Gateway" = 1 GROUP BY 1 HAVING count(*) > 1;

BEGIN;

INSERT INTO "Model" (
  "Id", "ModelProviderId", "Name", "ApiModelId", "Gateway",
  "ReleaseDate", "KnowledgeCutoffUtc", "CutoffEvidence", "CutoffVerifiedUtc",
  "InputCostPerMTok", "OutputCostPerMTok", "IsActive", "IsDefault",
  "CreatedUtc", "CreatedBy")
VALUES
  ('b0000000-0000-0000-0000-000000000009', 'a0000000-0000-0000-0000-000000000006',
   'Qwen 3.8 Max (0902)', 'qwen/qwen3.8-max-0902', 1,
   '2026-09-03', NULL, 'No published cutoff (Sep 2026 release; replaces Qwen 3 Max, retired by OpenRouter 2026-10-09) — NULL = treated higher-risk by design', NULL,
   2.00, 6.00, TRUE, FALSE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000')
ON CONFLICT DO NOTHING;

-- The insert skips silently on any unique clash, so assert the seat is
-- actually filled, by a row the lab can reach (Gateway 1 = OpenRouter, the
-- only route AiModelClientResolver.CanResolve accepts), before vacating the
-- old one. Raising aborts the
-- transaction: Qwen 3 Max stays active and nothing changes.
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM "Model" m
    JOIN "ModelProvider" p ON p."Id" = m."ModelProviderId"
    WHERE m."ApiModelId" = 'qwen/qwen3.8-max-0902' AND m."Gateway" = 1 AND m."IsActive" AND p."IsActive") THEN
    RAISE EXCEPTION 'Qwen 3.8 Max (0902) is not present, active and routed via OpenRouter; refusing to deactivate Qwen 3 Max';
  END IF;
END $$;

UPDATE "Model"
SET "IsActive" = FALSE,
    "ModifiedUtc" = NOW() AT TIME ZONE 'utc',
    "ModifiedBy" = '00000000-0000-0000-0000-000000000000'
WHERE "ApiModelId" = 'qwen/qwen3-max' AND "IsActive";

-- Expect: Qwen 3.8 Max (0902) active, Qwen 3 Max inactive.
SELECT "Name", "ApiModelId", "IsActive" FROM "Model" WHERE "ApiModelId" LIKE 'qwen/%' ORDER BY "Name";

COMMIT;
