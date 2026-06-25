using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Notification.Migrations
{
    /// <inheritdoc />
    public partial class AddPickemGroupMatchupProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PickemGroupMatchups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonWeek = table.Column<int>(type: "integer", nullable: false),
                    StatusTypeName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroupMatchups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupMatchups_ContestId",
                table: "PickemGroupMatchups",
                column: "ContestId");

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupMatchups_PickemGroupId_ContestId",
                table: "PickemGroupMatchups",
                columns: new[] { "PickemGroupId", "ContestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PickemGroupMatchups");
        }
    }
}
