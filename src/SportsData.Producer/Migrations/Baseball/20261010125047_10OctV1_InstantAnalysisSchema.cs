using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class _10OctV1_InstantAnalysisSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LiveAnalysisEnabled",
                table: "CompetitionStream",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PlayAnalysis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    FactType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Facts = table.Column<string>(type: "jsonb", nullable: false),
                    Text = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayAnalysis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayAnalysis_CompetitionPlay_PlayId",
                        column: x => x.PlayId,
                        principalTable: "CompetitionPlay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayAnalysis_CompetitionId_PlayId",
                table: "PlayAnalysis",
                columns: new[] { "CompetitionId", "PlayId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayAnalysis_PlayId_AnalysisRunId_Ordinal",
                table: "PlayAnalysis",
                columns: new[] { "PlayId", "AnalysisRunId", "Ordinal" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayAnalysis");

            migrationBuilder.DropColumn(
                name: "LiveAnalysisEnabled",
                table: "CompetitionStream");
        }
    }
}
