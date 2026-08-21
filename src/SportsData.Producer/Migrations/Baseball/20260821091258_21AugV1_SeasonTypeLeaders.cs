using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class _21AugV1_SeasonTypeLeaders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeasonTypeLeader",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonTypeCode = table.Column<int>(type: "integer", nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CategoryDisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    DisplayValue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AthleteSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonTypeLeader", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonTypeLeader_AthleteSeason_AthleteSeasonId",
                        column: x => x.AthleteSeasonId,
                        principalTable: "AthleteSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeasonTypeLeader_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonTypeLeader_AthleteSeasonId",
                table: "SeasonTypeLeader",
                column: "AthleteSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonTypeLeader_FranchiseSeasonId",
                table: "SeasonTypeLeader",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonTypeLeader_SeasonYear_SeasonTypeCode_CategoryName",
                table: "SeasonTypeLeader",
                columns: new[] { "SeasonYear", "SeasonTypeCode", "CategoryName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeasonTypeLeader");
        }
    }
}
