using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
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
                defaultValue: "BaseballContest");

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

            migrationBuilder.AlterColumn<string>(
                name: "ClockDisplayValue",
                table: "CompetitionPlay",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AtBatId",
                table: "CompetitionPlay",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AtBatPitchNumber",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayErrors",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayHits",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BatOrder",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatsAbbreviation",
                table: "CompetitionPlay",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatsType",
                table: "CompetitionPlay",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "CompetitionPlay",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "BaseballCompetitionPlay");

            migrationBuilder.AddColumn<double>(
                name: "HitCoordinateX",
                table: "CompetitionPlay",
                type: "double precision",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HitCoordinateY",
                table: "CompetitionPlay",
                type: "double precision",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeErrors",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeHits",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDoublePlay",
                table: "CompetitionPlay",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTriplePlay",
                table: "CompetitionPlay",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PitchCoordinateX",
                table: "CompetitionPlay",
                type: "double precision",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PitchCoordinateY",
                table: "CompetitionPlay",
                type: "double precision",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PitchCountBalls",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PitchCountStrikes",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PitchTypeAbbreviation",
                table: "CompetitionPlay",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PitchTypeId",
                table: "CompetitionPlay",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PitchTypeText",
                table: "CompetitionPlay",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PitchVelocity",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RbiCount",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResultCountBalls",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResultCountStrikes",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StrikeType",
                table: "CompetitionPlay",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SummaryType",
                table: "CompetitionPlay",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Trajectory",
                table: "CompetitionPlay",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "Competition",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "BaseballCompetition");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "Contest");

            migrationBuilder.DropColumn(
                name: "AtBatId",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "AtBatPitchNumber",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "AwayErrors",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "AwayHits",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "BatOrder",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "BatsAbbreviation",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "BatsType",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "HitCoordinateX",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "HitCoordinateY",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "HomeErrors",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "HomeHits",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "IsDoublePlay",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "IsTriplePlay",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "PitchCoordinateX",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "PitchCoordinateY",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "PitchCountBalls",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "PitchCountStrikes",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "PitchTypeAbbreviation",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "PitchTypeId",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "PitchTypeText",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "PitchVelocity",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "RbiCount",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "ResultCountBalls",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "ResultCountStrikes",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "StrikeType",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "SummaryType",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "Trajectory",
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

            migrationBuilder.AlterColumn<string>(
                name: "ClockDisplayValue",
                table: "CompetitionPlay",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
