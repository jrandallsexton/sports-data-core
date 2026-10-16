using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class ModelReasoningCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReasoningEffort",
                table: "Model",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReasoningMaxTokens",
                table: "Model",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Model_ReasoningEffortOrMaxTokens",
                table: "Model",
                sql: "\"ReasoningEffort\" IS NULL OR \"ReasoningMaxTokens\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Model_ReasoningEffortOrMaxTokens",
                table: "Model");

            migrationBuilder.DropColumn(
                name: "ReasoningEffort",
                table: "Model");

            migrationBuilder.DropColumn(
                name: "ReasoningMaxTokens",
                table: "Model");
        }
    }
}
