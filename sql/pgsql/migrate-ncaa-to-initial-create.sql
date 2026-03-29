-- Run this against all NCAA Producer databases AFTER deploying the new migration code
-- and BEFORE scaling Producer pods back up.
--
-- Databases:
--   sdProducer.FootballNcaa
--
-- This tells EF Core that the new InitialCreate migration is already applied
-- (since the tables already exist) and removes the old squashed migration IDs
-- that no longer have corresponding migration files.

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260328204929_InitialCreate', '10.0.2')
ON CONFLICT DO NOTHING;

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" IN (
    '20260202101027_02FebV1_Baseline',
    '20260203234117_03FebV1_AthSeasonInj',
    '20260205112221_05FebV1_CoachSeasonRec',
    '20260205122422_05FebV2_CoachSeasonRec',
    '20260301101520_01MarV1_AthleteFranchSeason',
    '20260310131638_10MarV1_CompetitionOddsEnrichment',
    '20260314130035_14MarV1_CompPowerIndexUniqueConstraint'
);
