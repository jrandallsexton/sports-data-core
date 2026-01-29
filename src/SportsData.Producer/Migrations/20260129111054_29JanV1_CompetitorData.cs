using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _29JanV1_CompetitorData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompetitionCompetitorRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayValue = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Value = table.Column<double>(type: "double precision", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionCompetitorRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorRecord_CompetitionCompetitor_Competiti~",
                        column: x => x.CompetitionCompetitorId,
                        principalTable: "CompetitionCompetitor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionCompetitorRecordStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ShortDisplayName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Value = table.Column<double>(type: "double precision", nullable: true),
                    DisplayValue = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionCompetitorRecordStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorRecordStat_CompetitionCompetitorRecord~",
                        column: x => x.CompetitionCompetitorRecordId,
                        principalTable: "CompetitionCompetitorRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorRecord_CompetitionCompetitorId",
                table: "CompetitionCompetitorRecord",
                column: "CompetitionCompetitorId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorRecord_CompetitionCompetitorId_Type",
                table: "CompetitionCompetitorRecord",
                columns: new[] { "CompetitionCompetitorId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorRecordStat_CompetitionCompetitorRecord~",
                table: "CompetitionCompetitorRecordStat",
                column: "CompetitionCompetitorRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorRecordStat_Name",
                table: "CompetitionCompetitorRecordStat",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionCompetitorRecordStat");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorRecord");
        }
    }
}
