using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class LatestV8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PickemGroupContest");

            migrationBuilder.CreateTable(
                name: "PickemGroupWeek",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonWeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonWeek = table.Column<int>(type: "integer", nullable: false),
                    AreMatchupsGenerated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroupWeek", x => new { x.GroupId, x.SeasonWeekId });
                    table.ForeignKey(
                        name: "FK_PickemGroupWeek_PickemGroup_GroupId",
                        column: x => x.GroupId,
                        principalTable: "PickemGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PickemGroupMatchup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonWeek = table.Column<int>(type: "integer", nullable: false),
                    AwaySpread = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: true),
                    HomeSpread = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: true),
                    OverUnder = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: true),
                    SeasonWeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroupMatchup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PickemGroupMatchup_PickemGroupWeek_GroupId_SeasonWeekId",
                        columns: x => new { x.GroupId, x.SeasonWeekId },
                        principalTable: "PickemGroupWeek",
                        principalColumns: new[] { "GroupId", "SeasonWeekId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 18, 0, 17, 31, 718, DateTimeKind.Utc).AddTicks(1441), new DateTime(2025, 8, 18, 0, 17, 31, 718, DateTimeKind.Utc).AddTicks(1559) });

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupMatchup_GroupId_ContestId",
                table: "PickemGroupMatchup",
                columns: new[] { "GroupId", "ContestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupMatchup_GroupId_SeasonWeekId",
                table: "PickemGroupMatchup",
                columns: new[] { "GroupId", "SeasonWeekId" });

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupMatchup_GroupId_SeasonYear_SeasonWeek",
                table: "PickemGroupMatchup",
                columns: new[] { "GroupId", "SeasonYear", "SeasonWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupWeek_SeasonYear_SeasonWeek",
                table: "PickemGroupWeek",
                columns: new[] { "SeasonYear", "SeasonWeek" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PickemGroupMatchup");

            migrationBuilder.DropTable(
                name: "PickemGroupWeek");

            migrationBuilder.CreateTable(
                name: "PickemGroupContest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonWeek = table.Column<int>(type: "integer", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroupContest", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 10, 14, 4, 15, 387, DateTimeKind.Utc).AddTicks(1440), new DateTime(2025, 8, 10, 14, 4, 15, 387, DateTimeKind.Utc).AddTicks(1597) });

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupContest_PickemGroupId_ContestId",
                table: "PickemGroupContest",
                columns: new[] { "PickemGroupId", "ContestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupContest_PickemGroupId_SeasonYear_SeasonWeek",
                table: "PickemGroupContest",
                columns: new[] { "PickemGroupId", "SeasonYear", "SeasonWeek" });
        }
    }
}
