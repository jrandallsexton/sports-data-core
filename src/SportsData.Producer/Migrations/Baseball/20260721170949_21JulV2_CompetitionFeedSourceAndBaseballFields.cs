using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class _21JulV2_CompetitionFeedSourceAndBaseballFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Duration",
                table: "Competition",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Necessary",
                table: "Competition",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeOfDay",
                table: "Competition",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WasSuspended",
                table: "Competition",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "Necessary",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "TimeOfDay",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "WasSuspended",
                table: "Competition");
        }
    }
}
