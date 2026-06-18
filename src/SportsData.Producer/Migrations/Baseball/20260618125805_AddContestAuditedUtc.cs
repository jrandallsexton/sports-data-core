using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class AddContestAuditedUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AuditedUtc",
                table: "Contest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contest_AuditedUtc_Pending",
                table: "Contest",
                column: "FinalizedUtc",
                filter: "\"FinalizedUtc\" IS NOT NULL AND \"AuditedUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contest_AuditedUtc_Pending",
                table: "Contest");

            migrationBuilder.DropColumn(
                name: "AuditedUtc",
                table: "Contest");
        }
    }
}
