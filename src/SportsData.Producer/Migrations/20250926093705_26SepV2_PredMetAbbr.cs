using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _26SepV2_PredMetAbbr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PredictionMetric_Abbreviation",
                table: "PredictionMetric");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionMetric_Abbreviation",
                table: "PredictionMetric",
                column: "Abbreviation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PredictionMetric_Abbreviation",
                table: "PredictionMetric");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionMetric_Abbreviation",
                table: "PredictionMetric",
                column: "Abbreviation",
                unique: true);
        }
    }
}
