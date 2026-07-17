-- Recovery script: recreate CompetitionCompetitorRecordStat on
-- sdProducer.BaseballMlb after the sdprod-data-0 → sdprod-data-01 PG
-- migration. The original salvage (pg_dump with --exclude-table) had
-- to skip this table because of a bad block on data-0's drive, so the
-- table is missing on data-01 even though __EFMigrationsHistory still
-- says InitialCreate applied.
--
-- Schema derived from:
--   src/SportsData.Producer/Migrations/Baseball/
--     20260407215622_InitialCreate.cs (lines 2425-2453, 4323-4331)
--
-- Connection string for executing this:
--   PGPASSWORD='<postgres-pw>' psql -h 127.0.0.1 -U postgres \
--     -d 'sdProducer.BaseballMlb' \
--     -f sql/pgsql/recreate_CompetitionCompetitorRecordStat_mlb.sql
--
-- Note on identifier names: EF's auto-generated FK/index names blow
-- past Postgres's 63-char limit and end up with a `~` truncation
-- marker (see migration source). This script uses shorter, readable
-- names instead — functionally equivalent at runtime. If a future
-- `dotnet ef migrations script` is run against this DB, it may
-- notice the name drift; low risk in practice.

CREATE TABLE "CompetitionCompetitorRecordStat" (
    "Id" uuid NOT NULL,
    "CompetitionCompetitorRecordId" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "DisplayName" character varying(200) NULL,
    "ShortDisplayName" character varying(50) NULL,
    "Description" character varying(500) NULL,
    "Abbreviation" character varying(20) NULL,
    "Type" character varying(50) NULL,
    "Value" double precision NULL,
    "DisplayValue" character varying(100) NULL,
    "CreatedUtc" timestamp with time zone NOT NULL,
    "ModifiedUtc" timestamp with time zone NULL,
    "CreatedBy" uuid NOT NULL,
    "ModifiedBy" uuid NULL,
    CONSTRAINT "PK_CompetitionCompetitorRecordStat" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CompetitionCompetitorRecordStat_CompetitorRecord"
        FOREIGN KEY ("CompetitionCompetitorRecordId")
        REFERENCES "CompetitionCompetitorRecord" ("Id")
        ON DELETE CASCADE
);

CREATE INDEX "IX_CCRecordStat_CompetitorRecordId"
    ON "CompetitionCompetitorRecordStat" ("CompetitionCompetitorRecordId");

CREATE INDEX "IX_CCRecordStat_Name"
    ON "CompetitionCompetitorRecordStat" ("Name");
