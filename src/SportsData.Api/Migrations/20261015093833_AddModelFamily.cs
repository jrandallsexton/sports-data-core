using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <summary>
    /// ModelFamily + Model.ModelFamilyId + one active lab seat per family
    /// (docs/features/model-family.md). Migrations auto-apply at API startup,
    /// so this one fixes data before adding constraints instead of failing:
    /// known models map by their fixed seed Ids, unknown ones get a family of
    /// their own, and a family with two active lab seats keeps its newest.
    /// </summary>
    public partial class AddModelFamily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelFamily",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelFamily", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelFamily_Name",
                table: "ModelFamily",
                column: "Name",
                unique: true);

            // Nullable until every row is mapped.
            migrationBuilder.AddColumn<Guid>(
                name: "ModelFamilyId",
                table: "Model",
                type: "uuid",
                nullable: true);

            // Seeded families (fixed Ids, matching sql/pgsql/seed_model_lab.sql).
            migrationBuilder.Sql("""
                INSERT INTO "ModelFamily" ("Id", "Name", "Description", "CreatedUtc", "CreatedBy")
                VALUES
                  ('c0000000-0000-0000-0000-000000000001', 'Claude Sonnet', NULL, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),
                  ('c0000000-0000-0000-0000-000000000002', 'Claude Haiku',  NULL, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),
                  ('c0000000-0000-0000-0000-000000000003', 'GPT',           NULL, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),
                  ('c0000000-0000-0000-0000-000000000004', 'Gemini',        NULL, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),
                  ('c0000000-0000-0000-0000-000000000005', 'Grok',          NULL, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),
                  ('c0000000-0000-0000-0000-000000000006', 'DeepSeek',      NULL, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),
                  ('c0000000-0000-0000-0000-000000000007', 'Qwen',          NULL, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000')
                ON CONFLICT DO NOTHING;
                """);

            // Known models by their fixed seed Ids.
            migrationBuilder.Sql("""
                UPDATE "Model" SET "ModelFamilyId" = CASE "Id"
                  WHEN 'b0000000-0000-0000-0000-000000000001' THEN 'c0000000-0000-0000-0000-000000000001'::uuid
                  WHEN 'b0000000-0000-0000-0000-000000000002' THEN 'c0000000-0000-0000-0000-000000000002'::uuid
                  WHEN 'b0000000-0000-0000-0000-000000000003' THEN 'c0000000-0000-0000-0000-000000000003'::uuid
                  WHEN 'b0000000-0000-0000-0000-000000000004' THEN 'c0000000-0000-0000-0000-000000000004'::uuid
                  WHEN 'b0000000-0000-0000-0000-000000000005' THEN 'c0000000-0000-0000-0000-000000000005'::uuid
                  WHEN 'b0000000-0000-0000-0000-000000000006' THEN 'c0000000-0000-0000-0000-000000000006'::uuid
                  WHEN 'b0000000-0000-0000-0000-000000000007' THEN 'c0000000-0000-0000-0000-000000000006'::uuid
                  WHEN 'b0000000-0000-0000-0000-000000000008' THEN 'c0000000-0000-0000-0000-000000000007'::uuid
                  WHEN 'b0000000-0000-0000-0000-000000000009' THEN 'c0000000-0000-0000-0000-000000000007'::uuid
                END
                WHERE "Id" IN (
                  'b0000000-0000-0000-0000-000000000001', 'b0000000-0000-0000-0000-000000000002',
                  'b0000000-0000-0000-0000-000000000003', 'b0000000-0000-0000-0000-000000000004',
                  'b0000000-0000-0000-0000-000000000005', 'b0000000-0000-0000-0000-000000000006',
                  'b0000000-0000-0000-0000-000000000007', 'b0000000-0000-0000-0000-000000000008',
                  'b0000000-0000-0000-0000-000000000009');
                """);

            // Models an operator created in the Model Manager: each joins the
            // family that shares its name, else gets a family of its own
            // (regroup later in the Model Manager).
            migrationBuilder.Sql("""
                DO $$
                DECLARE r RECORD; fid uuid;
                BEGIN
                  FOR r IN SELECT "Id", "Name" FROM "Model" WHERE "ModelFamilyId" IS NULL LOOP
                    SELECT "Id" INTO fid FROM "ModelFamily" WHERE "Name" = r."Name";
                    IF fid IS NULL THEN
                      fid := gen_random_uuid();
                      INSERT INTO "ModelFamily" ("Id", "Name", "Description", "CreatedUtc", "CreatedBy")
                      VALUES (fid, r."Name", 'Auto-created by the AddModelFamily migration; regroup in the Model Manager',
                              NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000');
                    END IF;
                    UPDATE "Model" SET "ModelFamilyId" = fid WHERE "Id" = r."Id";
                  END LOOP;
                END $$;
                """);

            // One active lab seat per family: keep the default, then the
            // newest release, active; deactivate the rest. Expected only where
            // model_lab_2026-10-14_qwen38_max.sql never ran (Qwen 3 Max beside
            // 3.8 Max), and the retired model 404s anyway.
            migrationBuilder.Sql("""
                DO $$
                DECLARE r RECORD;
                BEGIN
                  FOR r IN
                    SELECT "Id", "Name" FROM (
                      SELECT "Id", "Name", ROW_NUMBER() OVER (
                        PARTITION BY "ModelFamilyId"
                        ORDER BY "IsDefault" DESC, "ReleaseDate" DESC NULLS LAST, "CreatedUtc" DESC) AS rn
                      FROM "Model"
                      WHERE "IsActive" AND "Gateway" = 1) ranked
                    WHERE rn > 1
                  LOOP
                    UPDATE "Model"
                    SET "IsActive" = FALSE,
                        "ModifiedUtc" = NOW() AT TIME ZONE 'utc',
                        "ModifiedBy" = '00000000-0000-0000-0000-000000000000'
                    WHERE "Id" = r."Id";
                    RAISE NOTICE 'AddModelFamily: deactivated % (%), its family already has a newer active lab seat', r."Name", r."Id";
                  END LOOP;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ModelFamilyId",
                table: "Model",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Model_SingleActiveLabSeat",
                table: "Model",
                column: "ModelFamilyId",
                unique: true,
                filter: "\"IsActive\" AND \"Gateway\" = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Model_ModelFamily_ModelFamilyId",
                table: "Model",
                column: "ModelFamilyId",
                principalTable: "ModelFamily",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Model_ModelFamily_ModelFamilyId",
                table: "Model");

            migrationBuilder.DropIndex(
                name: "IX_Model_SingleActiveLabSeat",
                table: "Model");

            migrationBuilder.DropColumn(
                name: "ModelFamilyId",
                table: "Model");

            migrationBuilder.DropTable(
                name: "ModelFamily");
        }
    }
}
