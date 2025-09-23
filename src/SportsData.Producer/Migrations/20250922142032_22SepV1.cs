using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _22SepV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionPredictionValue_CompetitionPrediction_Competitio~",
                table: "CompetitionPredictionValue",
                column: "CompetitionPredictionId",
                principalTable: "CompetitionPrediction",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionPredictionValue_CompetitionPrediction_Competitio~",
                table: "CompetitionPredictionValue");
        }
    }
}
