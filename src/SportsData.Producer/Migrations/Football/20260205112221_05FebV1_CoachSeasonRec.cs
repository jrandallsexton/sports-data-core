using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _05FebV1_CoachSeasonRec : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoachSeasonRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Value = table.Column<double>(type: "double precision", nullable: true),
                    CoachSeasonId1 = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachSeasonRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachSeasonRecord_CoachSeason_CoachSeasonId",
                        column: x => x.CoachSeasonId,
                        principalTable: "CoachSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CoachSeasonRecord_CoachSeason_CoachSeasonId1",
                        column: x => x.CoachSeasonId1,
                        principalTable: "CoachSeason",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CoachSeasonRecordExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachSeasonRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachSeasonRecordExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachSeasonRecordExternalId_CoachSeasonRecord_CoachSeasonRe~",
                        column: x => x.CoachSeasonRecordId,
                        principalTable: "CoachSeasonRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoachSeasonRecordStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachSeasonRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ShortDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Abbreviation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Value = table.Column<double>(type: "double precision", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachSeasonRecordStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachSeasonRecordStat_CoachSeasonRecord_CoachSeasonRecordId",
                        column: x => x.CoachSeasonRecordId,
                        principalTable: "CoachSeasonRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoachSeasonRecord_CoachSeasonId",
                table: "CoachSeasonRecord",
                column: "CoachSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachSeasonRecord_CoachSeasonId1",
                table: "CoachSeasonRecord",
                column: "CoachSeasonId1");

            migrationBuilder.CreateIndex(
                name: "IX_CoachSeasonRecordExternalId_CoachSeasonRecordId",
                table: "CoachSeasonRecordExternalId",
                column: "CoachSeasonRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachSeasonRecordStat_CoachSeasonRecordId",
                table: "CoachSeasonRecordStat",
                column: "CoachSeasonRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoachSeasonRecordExternalId");

            migrationBuilder.DropTable(
                name: "CoachSeasonRecordStat");

            migrationBuilder.DropTable(
                name: "CoachSeasonRecord");
        }
    }
}
