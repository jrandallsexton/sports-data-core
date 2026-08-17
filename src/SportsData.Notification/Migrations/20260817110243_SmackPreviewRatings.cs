using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Notification.Migrations
{
    /// <inheritdoc />
    public partial class SmackPreviewRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmackPreviewRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    PickerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Voice = table.Column<int>(type: "integer", nullable: false),
                    Situation = table.Column<int>(type: "integer", nullable: false),
                    PhraseId = table.Column<Guid>(type: "uuid", nullable: true),
                    RenderedText = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Stars = table.Column<int>(type: "integer", nullable: false),
                    FactsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmackPreviewRatings", x => x.Id);
                    table.CheckConstraint("CK_SmackPreviewRatings_Stars_Range", "\"Stars\" BETWEEN 0 AND 4");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmackPreviewRatings_PickId_Voice",
                table: "SmackPreviewRatings",
                columns: new[] { "PickId", "Voice" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmackPreviewRatings");
        }
    }
}
