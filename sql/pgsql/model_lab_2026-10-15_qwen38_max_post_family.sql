-- Model Consensus Lab: Qwen 3 Max -> Qwen 3.8 Max (0902), for a database
-- where the AddModelFamily migration (#842) has ALREADY run. Use this in
-- place of model_lab_2026-10-14_qwen38_max.sql, which predates
-- Model.ModelFamilyId and fails NOT NULL once the migration has run.
-- (Prod 2026-10-15: the API deployed before the original swap ran.)
--
-- Same row as the original (fixed Id b...0009, matching seed_model_lab.sql),
-- now placed in the Qwen family (c...0007). Order matters: the old seat
-- holder is deactivated BEFORE the insert, because
-- IX_Model_SingleActiveLabSeat allows one active OpenRouter model per family
-- and PostgreSQL checks it per statement.
--
-- Target DB: API (AppDataContext). One transaction; any failure aborts it
-- and leaves Qwen 3 Max active. Re-runnable: the update is a no-op once
-- applied and the insert skips on any unique clash.

BEGIN;

-- Precondition: the migration ran and seeded the Qwen family.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "ModelFamily" WHERE "Id" = 'c0000000-0000-0000-0000-000000000007') THEN
    RAISE EXCEPTION 'Qwen family (c0000000-...-0007) not found; has the AddModelFamily migration run?';
  END IF;
END $$;

-- Vacate the seat first (by API id, so it works whatever family 3 Max landed in).
UPDATE "Model"
SET "IsActive" = FALSE,
    "ModifiedUtc" = NOW() AT TIME ZONE 'utc',
    "ModifiedBy" = '00000000-0000-0000-0000-000000000000'
WHERE "ApiModelId" = 'qwen/qwen3-max' AND "IsActive";

INSERT INTO "Model" (
  "Id", "ModelProviderId", "ModelFamilyId", "Name", "ApiModelId", "Gateway",
  "ReleaseDate", "KnowledgeCutoffUtc", "CutoffEvidence", "CutoffVerifiedUtc",
  "InputCostPerMTok", "OutputCostPerMTok", "IsActive", "IsDefault",
  "CreatedUtc", "CreatedBy")
VALUES
  ('b0000000-0000-0000-0000-000000000009', 'a0000000-0000-0000-0000-000000000006',
   'c0000000-0000-0000-0000-000000000007',
   'Qwen 3.8 Max (0902)', 'qwen/qwen3.8-max-0902', 1,
   '2026-09-03', NULL, 'No published cutoff (Sep 2026 release; replaces Qwen 3 Max, retired by OpenRouter 2026-10-09) — NULL = treated higher-risk by design', NULL,
   2.00, 6.00, TRUE, FALSE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000')
ON CONFLICT DO NOTHING;

-- The insert skips silently on any unique clash: assert the Qwen seat is
-- actually held by a lab-reachable 3.8 row before committing the vacate.
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM "Model" m
    JOIN "ModelProvider" p ON p."Id" = m."ModelProviderId"
    WHERE m."ApiModelId" = 'qwen/qwen3.8-max-0902' AND m."Gateway" = 1
      AND m."IsActive" AND p."IsActive"
      AND m."ModelFamilyId" = 'c0000000-0000-0000-0000-000000000007') THEN
    RAISE EXCEPTION 'Qwen 3.8 Max (0902) is not present, active, routed via OpenRouter and in the Qwen family; rolling back (Qwen 3 Max stays active)';
  END IF;
END $$;

-- Expect: Qwen 3.8 Max (0902) active in Qwen; Qwen 3 Max inactive.
SELECT f."Name" AS family, m."Name", m."ApiModelId", m."IsActive"
FROM "Model" m JOIN "ModelFamily" f ON f."Id" = m."ModelFamilyId"
WHERE m."ApiModelId" LIKE 'qwen/%' ORDER BY m."Name";

COMMIT;
