using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _29OctV1_CompMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompetitionMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChannelId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChannelTitle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    PublishedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ThumbnailDefaultUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ThumbnailDefaultWidth = table.Column<int>(type: "integer", nullable: false),
                    ThumbnailDefaultHeight = table.Column<int>(type: "integer", nullable: false),
                    ThumbnailMediumUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ThumbnailMediumWidth = table.Column<int>(type: "integer", nullable: false),
                    ThumbnailMediumHeight = table.Column<int>(type: "integer", nullable: false),
                    ThumbnailHighUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ThumbnailHighWidth = table.Column<int>(type: "integer", nullable: false),
                    ThumbnailHighHeight = table.Column<int>(type: "integer", nullable: false),
                    IsAdminPinned = table.Column<bool>(type: "boolean", nullable: false),
                    IsAutoIndexed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionMedia_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionMedia_FranchiseSeason_AwayFranchiseSeasonId",
                        column: x => x.AwayFranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionMedia_FranchiseSeason_HomeFranchiseSeasonId",
                        column: x => x.HomeFranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionMedia_AwayFranchiseSeasonId",
                table: "CompetitionMedia",
                column: "AwayFranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionMedia_CompetitionId",
                table: "CompetitionMedia",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionMedia_HomeFranchiseSeasonId",
                table: "CompetitionMedia",
                column: "HomeFranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionMedia_VideoId",
                table: "CompetitionMedia",
                column: "VideoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionMedia");
        }
    }
}
