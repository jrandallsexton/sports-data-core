using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Provider.Migrations
{
    /// <inheritdoc />
    public partial class EspnKnownBadUriReasonBackoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailureCount",
                table: "EspnKnownBadUri",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "EspnKnownBadUri",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "BadRequest");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureCount",
                table: "EspnKnownBadUri");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "EspnKnownBadUri");
        }
    }
}
