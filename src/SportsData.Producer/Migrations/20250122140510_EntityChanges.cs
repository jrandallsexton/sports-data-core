using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class EntityChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GlobalId",
                table: "VenueExternalId");

            migrationBuilder.DropColumn(
                name: "GlobalId",
                table: "Venue");

            migrationBuilder.DropColumn(
                name: "GlobalId",
                table: "GroupSeasonLogo");

            migrationBuilder.DropColumn(
                name: "GlobalId",
                table: "GroupSeason");

            migrationBuilder.DropColumn(
                name: "GlobalId",
                table: "GroupLogo");

            migrationBuilder.DropColumn(
                name: "GlobalId",
                table: "GroupExternalId");

            migrationBuilder.DropColumn(
                name: "GlobalId",
                table: "Group");

            migrationBuilder.DropColumn(
                name: "GlobalId",
                table: "FranchiseSeasonLogo");

            migrationBuilder.DropColumn(
                name: "GlobalId",
                table: "FranchiseSeason");

            migrationBuilder.DropColumn(
                name: "GlobalId",
                table: "FranchiseLogo");

            migrationBuilder.DropColumn(
                name: "GlobalId",
                table: "FranchiseExternalId");

            migrationBuilder.DropColumn(
                name: "GlobalId",
                table: "Franchise");

            migrationBuilder.AddColumn<string>(
                name: "ColorCodeAltHex",
                table: "Franchise",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorCodeAltHex",
                table: "Franchise");

            migrationBuilder.AddColumn<Guid>(
                name: "GlobalId",
                table: "VenueExternalId",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GlobalId",
                table: "Venue",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GlobalId",
                table: "GroupSeasonLogo",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GlobalId",
                table: "GroupSeason",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GlobalId",
                table: "GroupLogo",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GlobalId",
                table: "GroupExternalId",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GlobalId",
                table: "Group",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GlobalId",
                table: "FranchiseSeasonLogo",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GlobalId",
                table: "FranchiseSeason",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GlobalId",
                table: "FranchiseLogo",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GlobalId",
                table: "FranchiseExternalId",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GlobalId",
                table: "Franchise",
                type: "uniqueidentifier",
                nullable: true);
        }
    }
}
