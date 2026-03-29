-- Run this against all NCAA Producer databases AFTER deploying the new migration code
-- and BEFORE scaling Producer pods back up.
--
-- Databases:
--   sdProducer.FootballNcaa
--
-- This tells EF Core that the new InitialCreate migration is already applied
-- (since the tables already exist) and removes the old squashed migration IDs
-- that no longer have corresponding migration files.

-- Precondition: verify this is an existing Producer database, not a fresh one
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory') THEN
        RAISE EXCEPTION 'Missing __EFMigrationsHistory — this does not appear to be an existing Producer database';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'AthleteSeason') THEN
        RAISE EXCEPTION 'Missing AthleteSeason table — this does not appear to be an existing Producer database';
    END IF;
END $$;

BEGIN;

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260328204929_InitialCreate', '10.0.2')
ON CONFLICT DO NOTHING;

DELETE FROM public."__EFMigrationsHistory"
WHERE "MigrationId" IN (
    '20260202101027_02FebV1_Baseline',
    '20260203234117_03FebV1_AthSeasonInj',
    '20260205112221_05FebV1_CoachSeasonRec',
    '20260205122422_05FebV2_CoachSeasonRec',
    '20260301101520_01MarV1_AthleteFranchSeason',
    '20260310131638_10MarV1_CompetitionOddsEnrichment',
    '20260314130035_14MarV1_CompPowerIndexUniqueConstraint'
);

COMMIT;
