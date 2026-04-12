using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class SportSpecificEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "Contest",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "FootballContest");

            migrationBuilder.AlterColumn<int>(
                name: "StatYardage",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<double>(
                name: "ClockValue",
                table: "CompetitionPlay",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "CompetitionPlay",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "FootballCompetitionPlay");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "Competition",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "FootballCompetition");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "Contest");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "Competition");

            migrationBuilder.AlterColumn<int>(
                name: "StatYardage",
                table: "CompetitionPlay",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "ClockValue",
                table: "CompetitionPlay",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);
        }
    }
}
