using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _22JanV1_IsForDarkBg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsForDarkBg",
                table: "VenueImage",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsForDarkBg",
                table: "GroupSeasonLogo",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsForDarkBg",
                table: "FranchiseSeasonLogo",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsForDarkBg",
                table: "FranchiseLogo",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsForDarkBg",
                table: "AthleteImage",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsForDarkBg",
                table: "VenueImage");

            migrationBuilder.DropColumn(
                name: "IsForDarkBg",
                table: "GroupSeasonLogo");

            migrationBuilder.DropColumn(
                name: "IsForDarkBg",
                table: "FranchiseSeasonLogo");

            migrationBuilder.DropColumn(
                name: "IsForDarkBg",
                table: "FranchiseLogo");

            migrationBuilder.DropColumn(
                name: "IsForDarkBg",
                table: "AthleteImage");
        }
    }
}
