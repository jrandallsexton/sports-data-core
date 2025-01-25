using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class ILogoChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalUrlHash",
                table: "VenueImage",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OriginalUrlHash",
                table: "GroupSeasonLogo",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OriginalUrlHash",
                table: "GroupLogo",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OriginalUrlHash",
                table: "FranchiseSeasonLogo",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OriginalUrlHash",
                table: "FranchiseLogo",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_VenueImage_OriginalUrlHash",
                table: "VenueImage",
                column: "OriginalUrlHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VenueImage_OriginalUrlHash",
                table: "VenueImage");

            migrationBuilder.DropColumn(
                name: "OriginalUrlHash",
                table: "VenueImage");

            migrationBuilder.DropColumn(
                name: "OriginalUrlHash",
                table: "GroupSeasonLogo");

            migrationBuilder.DropColumn(
                name: "OriginalUrlHash",
                table: "GroupLogo");

            migrationBuilder.DropColumn(
                name: "OriginalUrlHash",
                table: "FranchiseSeasonLogo");

            migrationBuilder.DropColumn(
                name: "OriginalUrlHash",
                table: "FranchiseLogo");
        }
    }
}
