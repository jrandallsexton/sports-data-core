using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _16OctV1_CompSitDown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CompetitionSituation_Distance",
                table: "CompetitionSituation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CompetitionSituation_Down",
                table: "CompetitionSituation");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CompetitionSituation_Distance",
                table: "CompetitionSituation",
                sql: "\"Distance\" >= -110");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CompetitionSituation_Down",
                table: "CompetitionSituation",
                sql: "\"Down\" BETWEEN -1 AND 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CompetitionSituation_Distance",
                table: "CompetitionSituation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CompetitionSituation_Down",
                table: "CompetitionSituation");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CompetitionSituation_Distance",
                table: "CompetitionSituation",
                sql: "\"Distance\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CompetitionSituation_Down",
                table: "CompetitionSituation",
                sql: "\"Down\" BETWEEN 1 AND 4");
        }
    }
}
