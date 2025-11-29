using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class _29Nov_ArtMeta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SeasonWeekId",
                table: "Article",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeasonWeekNumber",
                table: "Article",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeasonYear",
                table: "Article",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeasonWeekId",
                table: "Article");

            migrationBuilder.DropColumn(
                name: "SeasonWeekNumber",
                table: "Article");

            migrationBuilder.DropColumn(
                name: "SeasonYear",
                table: "Article");
        }
    }
}
