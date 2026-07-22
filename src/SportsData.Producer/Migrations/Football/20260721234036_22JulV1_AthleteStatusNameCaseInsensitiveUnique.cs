using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _22JulV1_AthleteStatusNameCaseInsensitiveUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameNormalized",
                table: "AthleteStatus",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                computedColumnSql: "lower(\"Name\")",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_AthleteStatus_NameNormalized",
                table: "AthleteStatus",
                column: "NameNormalized",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AthleteStatus_NameNormalized",
                table: "AthleteStatus");

            migrationBuilder.DropColumn(
                name: "NameNormalized",
                table: "AthleteStatus");
        }
    }
}
