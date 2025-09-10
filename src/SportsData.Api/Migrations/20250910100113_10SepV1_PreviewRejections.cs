using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class _10SepV1_PreviewRejections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedUtc",
                table: "MatchupPreview",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedUtc",
                table: "MatchupPreview",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionNote",
                table: "MatchupPreview",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedUtc",
                table: "MatchupPreview");

            migrationBuilder.DropColumn(
                name: "RejectedUtc",
                table: "MatchupPreview");

            migrationBuilder.DropColumn(
                name: "RejectionNote",
                table: "MatchupPreview");
        }
    }
}
