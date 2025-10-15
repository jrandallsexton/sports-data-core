using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _15OctV1_Plays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Idempotent cleanup of old FK/Index/Column names (if they still exist) ----
            migrationBuilder.Sql(@"
DO $$
BEGIN
    -- Drop FK using old TeamFranchiseSeasonId if it exists
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CompetitionPlay_FranchiseSeason_TeamFranchiseSeasonId') THEN
        ALTER TABLE ""CompetitionPlay"" DROP CONSTRAINT ""FK_CompetitionPlay_FranchiseSeason_TeamFranchiseSeasonId"";
    END IF;

    -- Rename index on TeamFranchiseSeasonId if it exists
    IF EXISTS (SELECT 1 FROM pg_class WHERE relname = 'IX_CompetitionPlay_TeamFranchiseSeasonId') THEN
        ALTER INDEX ""IX_CompetitionPlay_TeamFranchiseSeasonId"" RENAME TO ""IX_CompetitionPlay_StartFranchiseSeasonId"";
    END IF;

    -- Some branches may have used StartTeamFranchiseSeasonId; rename that index too if present
    IF EXISTS (SELECT 1 FROM pg_class WHERE relname = 'IX_CompetitionPlay_StartTeamFranchiseSeasonId') THEN
        ALTER INDEX ""IX_CompetitionPlay_StartTeamFranchiseSeasonId"" RENAME TO ""IX_CompetitionPlay_StartFranchiseSeasonId"";
    END IF;

    -- Drop the legacy TeamFranchiseSeasonId column if it still exists
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'CompetitionPlay' AND column_name = 'TeamFranchiseSeasonId'
    ) THEN
        ALTER TABLE ""CompetitionPlay"" DROP COLUMN ""TeamFranchiseSeasonId"";
    END IF;
END $$;
");

            // ---- Rename StartTeamFranchiseSeasonId -> StartFranchiseSeasonId if needed ----
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'CompetitionPlay' AND column_name = 'StartTeamFranchiseSeasonId'
    ) THEN
        ALTER TABLE ""CompetitionPlay"" RENAME COLUMN ""StartTeamFranchiseSeasonId"" TO ""StartFranchiseSeasonId"";
    END IF;
END $$;
");

            // ---- Ensure StartFranchiseSeasonId column exists (no-op if already present) ----
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'CompetitionPlay' AND column_name = 'StartFranchiseSeasonId'
    ) THEN
        ALTER TABLE ""CompetitionPlay"" ADD COLUMN ""StartFranchiseSeasonId"" uuid NULL;
    END IF;
END $$;
");

            // ---- Add EndFranchiseSeasonId (as per your change) if missing ----
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'CompetitionPlay' AND column_name = 'EndFranchiseSeasonId'
    ) THEN
        ALTER TABLE ""CompetitionPlay"" ADD COLUMN ""EndFranchiseSeasonId"" uuid NULL;
    END IF;
END $$;
");

            // ---- Recreate FK for StartFranchiseSeasonId if missing ----
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CompetitionPlay_FranchiseSeason_StartFranchiseSeasonId') THEN
        ALTER TABLE ""CompetitionPlay""
        ADD CONSTRAINT ""FK_CompetitionPlay_FranchiseSeason_StartFranchiseSeasonId""
        FOREIGN KEY (""StartFranchiseSeasonId"")
        REFERENCES ""FranchiseSeason"" (""Id"")
        ON DELETE RESTRICT;
    END IF;
END $$;
");

            // ---- (Optional) FK for EndFranchiseSeasonId if you intend one; keep or remove as needed ----
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CompetitionPlay_FranchiseSeason_EndFranchiseSeasonId') THEN
        ALTER TABLE ""CompetitionPlay""
        ADD CONSTRAINT ""FK_CompetitionPlay_FranchiseSeason_EndFranchiseSeasonId""
        FOREIGN KEY (""EndFranchiseSeasonId"")
        REFERENCES ""FranchiseSeason"" (""Id"")
        ON DELETE RESTRICT;
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ---- Drop FKs that reference the new columns (if present) ----
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CompetitionPlay_FranchiseSeason_EndFranchiseSeasonId') THEN
        ALTER TABLE ""CompetitionPlay"" DROP CONSTRAINT ""FK_CompetitionPlay_FranchiseSeason_EndFranchiseSeasonId"";
    END IF;
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CompetitionPlay_FranchiseSeason_StartFranchiseSeasonId') THEN
        ALTER TABLE ""CompetitionPlay"" DROP CONSTRAINT ""FK_CompetitionPlay_FranchiseSeason_StartFranchiseSeasonId"";
    END IF;
END $$;
");

            // ---- Drop EndFranchiseSeasonId if present ----
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'CompetitionPlay' AND column_name = 'EndFranchiseSeasonId'
    ) THEN
        ALTER TABLE ""CompetitionPlay"" DROP COLUMN ""EndFranchiseSeasonId"";
    END IF;
END $$;
");

            // ---- Rename StartFranchiseSeasonId back to StartTeamFranchiseSeasonId if present ----
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'CompetitionPlay' AND column_name = 'StartFranchiseSeasonId'
    ) THEN
        ALTER TABLE ""CompetitionPlay"" RENAME COLUMN ""StartFranchiseSeasonId"" TO ""StartTeamFranchiseSeasonId"";
    END IF;
END $$;
");

            // ---- Recreate the old TeamFranchiseSeasonId column (nullable false with default as in your original Down) ----
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'CompetitionPlay' AND column_name = 'TeamFranchiseSeasonId'
    ) THEN
        ALTER TABLE ""CompetitionPlay""
        ADD COLUMN ""TeamFranchiseSeasonId"" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $$;
");

            // ---- Recreate old FK on TeamFranchiseSeasonId if desired ----
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CompetitionPlay_FranchiseSeason_TeamFranchiseSeasonId') THEN
        ALTER TABLE ""CompetitionPlay""
        ADD CONSTRAINT ""FK_CompetitionPlay_FranchiseSeason_TeamFranchiseSeasonId""
        FOREIGN KEY (""TeamFranchiseSeasonId"")
        REFERENCES ""FranchiseSeason"" (""Id"")
        ON DELETE RESTRICT;
    END IF;
END $$;
");

            // ---- Rename indexes back if needed ----
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_class WHERE relname = 'IX_CompetitionPlay_StartFranchiseSeasonId') THEN
        ALTER INDEX ""IX_CompetitionPlay_StartFranchiseSeasonId"" RENAME TO ""IX_CompetitionPlay_TeamFranchiseSeasonId"";
    END IF;
END $$;
");
        }
    }
}
