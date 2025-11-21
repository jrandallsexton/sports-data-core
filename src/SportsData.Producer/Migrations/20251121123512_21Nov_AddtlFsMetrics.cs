using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _21Nov_AddtlFsMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MarginLossAvg",
                table: "FranchiseSeason",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarginLossMax",
                table: "FranchiseSeason",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarginLossMin",
                table: "FranchiseSeason",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarginWinAvg",
                table: "FranchiseSeason",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarginWinMax",
                table: "FranchiseSeason",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarginWinMin",
                table: "FranchiseSeason",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PtsAllowedAvg",
                table: "FranchiseSeason",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PtsAllowedMax",
                table: "FranchiseSeason",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PtsAllowedMin",
                table: "FranchiseSeason",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PtsScoredAvg",
                table: "FranchiseSeason",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PtsScoredMax",
                table: "FranchiseSeason",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PtsScoredMin",
                table: "FranchiseSeason",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarginLossAvg",
                table: "FranchiseSeason");

            migrationBuilder.DropColumn(
                name: "MarginLossMax",
                table: "FranchiseSeason");

            migrationBuilder.DropColumn(
                name: "MarginLossMin",
                table: "FranchiseSeason");

            migrationBuilder.DropColumn(
                name: "MarginWinAvg",
                table: "FranchiseSeason");

            migrationBuilder.DropColumn(
                name: "MarginWinMax",
                table: "FranchiseSeason");

            migrationBuilder.DropColumn(
                name: "MarginWinMin",
                table: "FranchiseSeason");

            migrationBuilder.DropColumn(
                name: "PtsAllowedAvg",
                table: "FranchiseSeason");

            migrationBuilder.DropColumn(
                name: "PtsAllowedMax",
                table: "FranchiseSeason");

            migrationBuilder.DropColumn(
                name: "PtsAllowedMin",
                table: "FranchiseSeason");

            migrationBuilder.DropColumn(
                name: "PtsScoredAvg",
                table: "FranchiseSeason");

            migrationBuilder.DropColumn(
                name: "PtsScoredMax",
                table: "FranchiseSeason");

            migrationBuilder.DropColumn(
                name: "PtsScoredMin",
                table: "FranchiseSeason");
        }
    }
}
