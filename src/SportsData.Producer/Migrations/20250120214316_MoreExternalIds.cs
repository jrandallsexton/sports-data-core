using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class MoreExternalIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FranchiseExternalId_Franchise_FranchiseId",
                table: "FranchiseExternalId");

            migrationBuilder.DropForeignKey(
                name: "FK_VenueExternalId_Venue_VenueId",
                table: "VenueExternalId");

            migrationBuilder.AlterColumn<Guid>(
                name: "VenueId",
                table: "VenueExternalId",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "FranchiseId",
                table: "FranchiseExternalId",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FranchiseExternalId_Franchise_FranchiseId",
                table: "FranchiseExternalId",
                column: "FranchiseId",
                principalTable: "Franchise",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VenueExternalId_Venue_VenueId",
                table: "VenueExternalId",
                column: "VenueId",
                principalTable: "Venue",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FranchiseExternalId_Franchise_FranchiseId",
                table: "FranchiseExternalId");

            migrationBuilder.DropForeignKey(
                name: "FK_VenueExternalId_Venue_VenueId",
                table: "VenueExternalId");

            migrationBuilder.AlterColumn<Guid>(
                name: "VenueId",
                table: "VenueExternalId",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "FranchiseId",
                table: "FranchiseExternalId",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddForeignKey(
                name: "FK_FranchiseExternalId_Franchise_FranchiseId",
                table: "FranchiseExternalId",
                column: "FranchiseId",
                principalTable: "Franchise",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VenueExternalId_Venue_VenueId",
                table: "VenueExternalId",
                column: "VenueId",
                principalTable: "Venue",
                principalColumn: "Id");
        }
    }
}
