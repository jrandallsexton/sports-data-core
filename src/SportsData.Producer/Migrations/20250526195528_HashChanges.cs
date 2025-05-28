using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class HashChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OriginalUrlHash",
                table: "VenueImage",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "Venue",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Venue",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Venue",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Venue",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Venue",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Venue",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalUrlHash",
                table: "GroupSeasonLogo",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalUrlHash",
                table: "GroupLogo",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalUrlHash",
                table: "FranchiseSeasonLogo",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalUrlHash",
                table: "FranchiseLogo",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalUrlHash",
                table: "AthleteImage",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_GroupSeasonLogo_OriginalUrlHash",
                table: "GroupSeasonLogo",
                column: "OriginalUrlHash");

            migrationBuilder.CreateIndex(
                name: "IX_GroupLogo_OriginalUrlHash",
                table: "GroupLogo",
                column: "OriginalUrlHash");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseLogo_OriginalUrlHash",
                table: "FranchiseLogo",
                column: "OriginalUrlHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GroupSeasonLogo_OriginalUrlHash",
                table: "GroupSeasonLogo");

            migrationBuilder.DropIndex(
                name: "IX_GroupLogo_OriginalUrlHash",
                table: "GroupLogo");

            migrationBuilder.DropIndex(
                name: "IX_FranchiseLogo_OriginalUrlHash",
                table: "FranchiseLogo");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Venue");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Venue");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Venue");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Venue");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Venue");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Venue");

            migrationBuilder.AlterColumn<int>(
                name: "OriginalUrlHash",
                table: "VenueImage",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<int>(
                name: "OriginalUrlHash",
                table: "GroupSeasonLogo",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<int>(
                name: "OriginalUrlHash",
                table: "GroupLogo",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<int>(
                name: "OriginalUrlHash",
                table: "FranchiseSeasonLogo",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "OriginalUrlHash",
                table: "FranchiseLogo",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<int>(
                name: "OriginalUrlHash",
                table: "AthleteImage",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }
    }
}
