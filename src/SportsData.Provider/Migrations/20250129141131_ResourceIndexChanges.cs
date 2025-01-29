using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Provider.Migrations
{
    /// <inheritdoc />
    public partial class ResourceIndexChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanonicalId",
                table: "ResourceIndex");

            migrationBuilder.AddColumn<bool>(
                name: "IsSeasonSpecific",
                table: "ResourceIndex",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSeasonSpecific",
                table: "ResourceIndex");

            migrationBuilder.AddColumn<Guid>(
                name: "CanonicalId",
                table: "ResourceIndex",
                type: "uniqueidentifier",
                nullable: true);
        }
    }
}
