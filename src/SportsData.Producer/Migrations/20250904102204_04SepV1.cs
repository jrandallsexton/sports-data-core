using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _04SepV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeasonRankingEntryStat");

            migrationBuilder.DropTable(
                name: "SeasonRankingExternalId");

            migrationBuilder.DropTable(
                name: "SeasonRankingEntry");

            migrationBuilder.DropTable(
                name: "SeasonRanking");

            migrationBuilder.AddColumn<Guid>(
                name: "PollId",
                table: "SeasonPollExternalId",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "SeasonPoll",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<Guid>(
                name: "SeasonWeekId",
                table: "FranchiseSeasonRanking",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SeasonPollWeek",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonPollId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonWeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurrenceNumber = table.Column<int>(type: "integer", nullable: false),
                    OccurrenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OccurrenceIsLast = table.Column<bool>(type: "boolean", nullable: false),
                    OccurrenceValue = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OccurrenceDisplay = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ShortName = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Headline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ShortHeadline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonPollWeek", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonPollWeek_SeasonPoll_SeasonPollId",
                        column: x => x.SeasonPollId,
                        principalTable: "SeasonPoll",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeasonPollWeek_SeasonWeek_SeasonWeekId",
                        column: x => x.SeasonWeekId,
                        principalTable: "SeasonWeek",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonPollWeekEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonPollWeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceList = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Current = table.Column<int>(type: "integer", nullable: false),
                    Previous = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<double>(type: "double precision", precision: 18, scale: 6, nullable: false),
                    FirstPlaceVotes = table.Column<int>(type: "integer", nullable: false),
                    Trend = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    IsOtherReceivingVotes = table.Column<bool>(type: "boolean", nullable: false),
                    IsDroppedOut = table.Column<bool>(type: "boolean", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordSummary = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Wins = table.Column<int>(type: "integer", nullable: true),
                    Losses = table.Column<int>(type: "integer", nullable: true),
                    RowDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowLastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonPollWeekEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonPollWeekEntry_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeasonPollWeekEntry_SeasonPollWeek_SeasonPollWeekId",
                        column: x => x.SeasonPollWeekId,
                        principalTable: "SeasonPollWeek",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonPollWeekExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonPollWeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonPollWeekExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonPollWeekExternalId_SeasonPollWeek_SeasonPollWeekId",
                        column: x => x.SeasonPollWeekId,
                        principalTable: "SeasonPollWeek",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeasonPollWeekExternalId_SeasonPollWeek_WeekId",
                        column: x => x.WeekId,
                        principalTable: "SeasonPollWeek",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonPollWeekEntryStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonPollWeekEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    DisplayValue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonPollWeekEntryStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonPollWeekEntryStat_SeasonPollWeekEntry_SeasonPollWeekE~",
                        column: x => x.SeasonPollWeekEntryId,
                        principalTable: "SeasonPollWeekEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonPollExternalId_PollId",
                table: "SeasonPollExternalId",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRanking_SeasonWeekId",
                table: "FranchiseSeasonRanking",
                column: "SeasonWeekId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonPollWeek_SeasonPollId_SeasonWeekId",
                table: "SeasonPollWeek",
                columns: new[] { "SeasonPollId", "SeasonWeekId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonPollWeek_SeasonWeekId",
                table: "SeasonPollWeek",
                column: "SeasonWeekId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonPollWeekEntry_FranchiseSeasonId",
                table: "SeasonPollWeekEntry",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonPollWeekEntry_SeasonPollWeekId_FranchiseSeasonId_Sour~",
                table: "SeasonPollWeekEntry",
                columns: new[] { "SeasonPollWeekId", "FranchiseSeasonId", "SourceList" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonPollWeekEntryStat_SeasonPollWeekEntryId_Name_Type",
                table: "SeasonPollWeekEntryStat",
                columns: new[] { "SeasonPollWeekEntryId", "Name", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonPollWeekExternalId_SeasonPollWeekId",
                table: "SeasonPollWeekExternalId",
                column: "SeasonPollWeekId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonPollWeekExternalId_WeekId",
                table: "SeasonPollWeekExternalId",
                column: "WeekId");

            migrationBuilder.AddForeignKey(
                name: "FK_FranchiseSeasonRanking_SeasonWeek_SeasonWeekId",
                table: "FranchiseSeasonRanking",
                column: "SeasonWeekId",
                principalTable: "SeasonWeek",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SeasonPollExternalId_SeasonPoll_PollId",
                table: "SeasonPollExternalId",
                column: "PollId",
                principalTable: "SeasonPoll",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FranchiseSeasonRanking_SeasonWeek_SeasonWeekId",
                table: "FranchiseSeasonRanking");

            migrationBuilder.DropForeignKey(
                name: "FK_SeasonPollExternalId_SeasonPoll_PollId",
                table: "SeasonPollExternalId");

            migrationBuilder.DropTable(
                name: "SeasonPollWeekEntryStat");

            migrationBuilder.DropTable(
                name: "SeasonPollWeekExternalId");

            migrationBuilder.DropTable(
                name: "SeasonPollWeekEntry");

            migrationBuilder.DropTable(
                name: "SeasonPollWeek");

            migrationBuilder.DropIndex(
                name: "IX_SeasonPollExternalId_PollId",
                table: "SeasonPollExternalId");

            migrationBuilder.DropIndex(
                name: "IX_FranchiseSeasonRanking_SeasonWeekId",
                table: "FranchiseSeasonRanking");

            migrationBuilder.DropColumn(
                name: "PollId",
                table: "SeasonPollExternalId");

            migrationBuilder.DropColumn(
                name: "SeasonWeekId",
                table: "FranchiseSeasonRanking");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "SeasonPoll",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "SeasonRanking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonWeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Headline = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OccurrenceDisplay = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OccurrenceIsLast = table.Column<bool>(type: "boolean", nullable: false),
                    OccurrenceNumber = table.Column<int>(type: "integer", nullable: false),
                    OccurrenceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OccurrenceValue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PollName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PollShortName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PollType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderPollId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ShortHeadline = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonRanking", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonRanking_SeasonWeek_SeasonWeekId",
                        column: x => x.SeasonWeekId,
                        principalTable: "SeasonWeek",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonRankingEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonRankingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Current = table.Column<int>(type: "integer", nullable: false),
                    FirstPlaceVotes = table.Column<int>(type: "integer", nullable: false),
                    IsOtherReceivingVotes = table.Column<bool>(type: "boolean", nullable: false),
                    Losses = table.Column<int>(type: "integer", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Points = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Previous = table.Column<int>(type: "integer", nullable: false),
                    RecordSummary = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RowDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowLastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceList = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Trend = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonRankingEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonRankingEntry_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeasonRankingEntry_SeasonRanking_SeasonRankingId",
                        column: x => x.SeasonRankingId,
                        principalTable: "SeasonRanking",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonRankingExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonRankingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    SourceUrlHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonRankingExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonRankingExternalId_SeasonRanking_SeasonRankingId",
                        column: x => x.SeasonRankingId,
                        principalTable: "SeasonRanking",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonRankingEntryStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonRankingEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayValue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(10,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonRankingEntryStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonRankingEntryStat_SeasonRankingEntry_SeasonRankingEntr~",
                        column: x => x.SeasonRankingEntryId,
                        principalTable: "SeasonRankingEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonRanking_SeasonWeekId_ProviderPollId_DateUtc",
                table: "SeasonRanking",
                columns: new[] { "SeasonWeekId", "ProviderPollId", "DateUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonRankingEntry_FranchiseSeasonId",
                table: "SeasonRankingEntry",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonRankingEntry_SeasonRankingId_FranchiseSeasonId_Source~",
                table: "SeasonRankingEntry",
                columns: new[] { "SeasonRankingId", "FranchiseSeasonId", "SourceList" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonRankingEntryStat_SeasonRankingEntryId_Name_Type",
                table: "SeasonRankingEntryStat",
                columns: new[] { "SeasonRankingEntryId", "Name", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonRankingExternalId_SeasonRankingId",
                table: "SeasonRankingExternalId",
                column: "SeasonRankingId");
        }
    }
}
