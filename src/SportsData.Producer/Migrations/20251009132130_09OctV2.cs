using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _09OctV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionPlay_Drive_DriveId",
                table: "CompetitionPlay");

            migrationBuilder.DropTable(
                name: "DriveExternalId");

            migrationBuilder.DropTable(
                name: "Drive");

            migrationBuilder.CreateTable(
                name: "CompetitionDrive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    SequenceNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ShortDisplayResult = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayResult = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Yards = table.Column<int>(type: "integer", nullable: false),
                    OffensivePlays = table.Column<int>(type: "integer", nullable: false),
                    IsScore = table.Column<bool>(type: "boolean", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SourceDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartPeriodType = table.Column<string>(type: "text", nullable: true),
                    StartPeriodNumber = table.Column<int>(type: "integer", nullable: true),
                    StartClockValue = table.Column<double>(type: "double precision", nullable: true),
                    StartClockDisplayValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StartYardLine = table.Column<int>(type: "integer", nullable: true),
                    StartText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartDown = table.Column<int>(type: "integer", nullable: true),
                    StartDistance = table.Column<int>(type: "integer", nullable: true),
                    StartYardsToEndzone = table.Column<int>(type: "integer", nullable: true),
                    StartFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartDownDistanceText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StartShortDownDistanceText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EndPeriodType = table.Column<string>(type: "text", nullable: true),
                    EndPeriodNumber = table.Column<int>(type: "integer", nullable: true),
                    EndClockValue = table.Column<double>(type: "double precision", nullable: true),
                    EndClockDisplayValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EndYardLine = table.Column<int>(type: "integer", nullable: true),
                    EndText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EndDown = table.Column<int>(type: "integer", nullable: true),
                    EndDistance = table.Column<int>(type: "integer", nullable: true),
                    EndYardsToEndzone = table.Column<int>(type: "integer", nullable: true),
                    EndFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    EndDownDistanceText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EndShortDownDistanceText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TimeElapsedDisplay = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TimeElapsedValue = table.Column<double>(type: "double precision", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionDrive", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionDrive_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionDriveExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriveId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CompetitionDriveExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionDriveExternalId_CompetitionDrive_DriveId",
                        column: x => x.DriveId,
                        principalTable: "CompetitionDrive",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionDrive_CompetitionId",
                table: "CompetitionDrive",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionDriveExternalId_DriveId",
                table: "CompetitionDriveExternalId",
                column: "DriveId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionPlay_CompetitionDrive_DriveId",
                table: "CompetitionPlay",
                column: "DriveId",
                principalTable: "CompetitionDrive",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionPlay_CompetitionDrive_DriveId",
                table: "CompetitionPlay");

            migrationBuilder.DropTable(
                name: "CompetitionDriveExternalId");

            migrationBuilder.DropTable(
                name: "CompetitionDrive");

            migrationBuilder.CreateTable(
                name: "Drive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    DisplayResult = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EndClockDisplayValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EndClockValue = table.Column<double>(type: "double precision", nullable: true),
                    EndDistance = table.Column<int>(type: "integer", nullable: true),
                    EndDown = table.Column<int>(type: "integer", nullable: true),
                    EndDownDistanceText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EndFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    EndPeriodNumber = table.Column<int>(type: "integer", nullable: true),
                    EndPeriodType = table.Column<string>(type: "text", nullable: true),
                    EndShortDownDistanceText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EndText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EndYardLine = table.Column<int>(type: "integer", nullable: true),
                    EndYardsToEndzone = table.Column<int>(type: "integer", nullable: true),
                    IsScore = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OffensivePlays = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SequenceNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ShortDisplayResult = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SourceDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StartClockDisplayValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StartClockValue = table.Column<double>(type: "double precision", nullable: true),
                    StartDistance = table.Column<int>(type: "integer", nullable: true),
                    StartDown = table.Column<int>(type: "integer", nullable: true),
                    StartDownDistanceText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StartFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartPeriodNumber = table.Column<int>(type: "integer", nullable: true),
                    StartPeriodType = table.Column<string>(type: "text", nullable: true),
                    StartShortDownDistanceText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StartText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartYardLine = table.Column<int>(type: "integer", nullable: true),
                    StartYardsToEndzone = table.Column<int>(type: "integer", nullable: true),
                    TimeElapsedDisplay = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TimeElapsedValue = table.Column<double>(type: "double precision", nullable: true),
                    Yards = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drive", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Drive_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DriveExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriveId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriveExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriveExternalId_Drive_DriveId",
                        column: x => x.DriveId,
                        principalTable: "Drive",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drive_CompetitionId",
                table: "Drive",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_DriveExternalId_DriveId",
                table: "DriveExternalId",
                column: "DriveId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionPlay_Drive_DriveId",
                table: "CompetitionPlay",
                column: "DriveId",
                principalTable: "Drive",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
