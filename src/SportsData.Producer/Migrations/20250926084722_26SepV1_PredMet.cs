using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _26SepV1_PredMet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PredictionMetric_Abbreviation",
                table: "PredictionMetric",
                column: "Abbreviation",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PredictionMetric_Name",
                table: "PredictionMetric",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PredictionMetric_Name_DisplayName_ShortDisplayName_Abbrevia~",
                table: "PredictionMetric",
                columns: new[] { "Name", "DisplayName", "ShortDisplayName", "Abbreviation", "Description" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PredictionMetric_Abbreviation",
                table: "PredictionMetric");

            migrationBuilder.DropIndex(
                name: "IX_PredictionMetric_Name",
                table: "PredictionMetric");

            migrationBuilder.DropIndex(
                name: "IX_PredictionMetric_Name_DisplayName_ShortDisplayName_Abbrevia~",
                table: "PredictionMetric");
        }
    }
}
