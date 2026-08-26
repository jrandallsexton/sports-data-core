using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class PlayerLineupPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupType",
                table: "PickemGroup",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PlayerLineup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonWeek = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerLineup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerLineup_PickemGroup_PickemGroupId",
                        column: x => x.PickemGroupId,
                        principalTable: "PickemGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerLineupSlot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerLineupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotId = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    AthleteId = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TeamName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TeamSlug = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContestStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OpponentName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerLineupSlot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerLineupSlot_PlayerLineup_PlayerLineupId",
                        column: x => x.PlayerLineupId,
                        principalTable: "PlayerLineup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerLineup_PickemGroupId_UserId_SeasonYear_SeasonWeek",
                table: "PlayerLineup",
                columns: new[] { "PickemGroupId", "UserId", "SeasonYear", "SeasonWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerLineupSlot_PlayerLineupId_SlotId",
                table: "PlayerLineupSlot",
                columns: new[] { "PlayerLineupId", "SlotId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerLineupSlot");

            migrationBuilder.DropTable(
                name: "PlayerLineup");

            migrationBuilder.DropColumn(
                name: "GroupType",
                table: "PickemGroup");
        }
    }
}
