using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _29JanV2_TeamSeasonLeaders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FranchiseSeasonLeader",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaderCategoryId = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonLeader", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonLeader_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonLeader_lkLeaderCategory_LeaderCategoryId",
                        column: x => x.LeaderCategoryId,
                        principalTable: "lkLeaderCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonLeaderStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonLeaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonLeaderStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonLeaderStat_AthleteSeason_AthleteSeasonId",
                        column: x => x.AthleteSeasonId,
                        principalTable: "AthleteSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonLeaderStat_FranchiseSeasonLeader_FranchiseSe~",
                        column: x => x.FranchiseSeasonLeaderId,
                        principalTable: "FranchiseSeasonLeader",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonLeaderStat_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonLeader_FranchiseSeasonId",
                table: "FranchiseSeasonLeader",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonLeader_LeaderCategoryId",
                table: "FranchiseSeasonLeader",
                column: "LeaderCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonLeaderStat_AthleteSeasonId",
                table: "FranchiseSeasonLeaderStat",
                column: "AthleteSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonLeaderStat_FranchiseSeasonId",
                table: "FranchiseSeasonLeaderStat",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonLeaderStat_FranchiseSeasonLeaderId",
                table: "FranchiseSeasonLeaderStat",
                column: "FranchiseSeasonLeaderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FranchiseSeasonLeaderStat");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonLeader");
        }
    }
}
