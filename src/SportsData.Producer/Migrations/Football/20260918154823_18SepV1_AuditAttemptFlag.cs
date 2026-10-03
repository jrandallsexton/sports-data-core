using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _18SepV1_AuditAttemptFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contest_AuditedUtc_Pending",
                table: "Contest");

            migrationBuilder.AddColumn<int>(
                name: "AuditAttemptCount",
                table: "Contest",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "AuditFlaggedUtc",
                table: "Contest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contest_AuditedUtc_Pending",
                table: "Contest",
                column: "FinalizedUtc",
                filter: "\"FinalizedUtc\" IS NOT NULL AND \"AuditedUtc\" IS NULL AND \"AuditFlaggedUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contest_AuditedUtc_Pending",
                table: "Contest");

            migrationBuilder.DropColumn(
                name: "AuditAttemptCount",
                table: "Contest");

            migrationBuilder.DropColumn(
                name: "AuditFlaggedUtc",
                table: "Contest");

            migrationBuilder.CreateIndex(
                name: "IX_Contest_AuditedUtc_Pending",
                table: "Contest",
                column: "FinalizedUtc",
                filter: "\"FinalizedUtc\" IS NOT NULL AND \"AuditedUtc\" IS NULL");
        }
    }
}
