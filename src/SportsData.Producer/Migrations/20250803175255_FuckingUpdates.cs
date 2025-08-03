using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class FuckingUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Competitor");

            migrationBuilder.AddColumn<string>(
                name: "Abbreviation",
                table: "GroupSeason",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId1",
                table: "GroupSeason",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "MidsizeName",
                table: "GroupSeason",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "GroupSeason",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "GroupSeason",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "GroupSeason",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "AthleteSeason",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "AthleteSeason",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExperienceAbbreviation",
                table: "AthleteSeason",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExperienceDisplayValue",
                table: "AthleteSeason",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExperienceYears",
                table: "AthleteSeason",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "AthleteSeason",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeightDisplay",
                table: "AthleteSeason",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HeightIn",
                table: "AthleteSeason",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AthleteSeason",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Jersey",
                table: "AthleteSeason",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "AthleteSeason",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "AthleteSeason",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "AthleteSeason",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StatusId",
                table: "AthleteSeason",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeightDisplay",
                table: "AthleteSeason",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightLb",
                table: "AthleteSeason",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "AthleteSeasonExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AthleteSeasonExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteSeasonExternalId_AthleteSeason_AthleteSeasonId",
                        column: x => x.AthleteSeasonId,
                        principalTable: "AthleteSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthleteSeasonStatistic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SplitId = table.Column<string>(type: "text", nullable: false),
                    SplitName = table.Column<string>(type: "text", nullable: false),
                    SplitAbbreviation = table.Column<string>(type: "text", nullable: false),
                    SplitType = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteSeasonStatistic", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteSeasonStatistic_AthleteSeason_AthleteSeasonId",
                        column: x => x.AthleteSeasonId,
                        principalTable: "AthleteSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionCompetitor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    HomeAway = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Winner = table.Column<bool>(type: "boolean", nullable: false),
                    CuratedRankCurrent = table.Column<int>(type: "integer", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionCompetitor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitor_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonRanking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", maxLength: 40, nullable: false),
                    Headline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ShortHeadline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DefaultRanking = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", maxLength: 40, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonRanking", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonRanking_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonRanking_Franchise_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "Franchise",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupSeasonExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupSeasonId1 = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_GroupSeasonExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupSeasonExternalId_GroupSeason_GroupSeasonId",
                        column: x => x.GroupSeasonId,
                        principalTable: "GroupSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupSeasonExternalId_GroupSeason_GroupSeasonId1",
                        column: x => x.GroupSeasonId1,
                        principalTable: "GroupSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthleteSeasonStatisticCategory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonStatisticId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    ShortDisplayName = table.Column<string>(type: "text", nullable: false),
                    Abbreviation = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteSeasonStatisticCategory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteSeasonStatisticCategory_AthleteSeasonStatistic_Athle~",
                        column: x => x.AthleteSeasonStatisticId,
                        principalTable: "AthleteSeasonStatistic",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionCompetitorExternalIds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CompetitionCompetitorExternalIds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorExternalIds_CompetitionCompetitor_Comp~",
                        column: x => x.CompetitionCompetitorId,
                        principalTable: "CompetitionCompetitor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionCompetitorLineScore",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    DisplayValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SourceDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionCompetitorLineScore", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorLineScore_CompetitionCompetitor_Compet~",
                        column: x => x.CompetitionCompetitorId,
                        principalTable: "CompetitionCompetitor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionCompetitorScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    DisplayValue = table.Column<string>(type: "text", nullable: false),
                    Winner = table.Column<bool>(type: "boolean", nullable: false),
                    SourceId = table.Column<string>(type: "text", nullable: false),
                    SourceDescription = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionCompetitorScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorScores_CompetitionCompetitor_Competiti~",
                        column: x => x.CompetitionCompetitorId,
                        principalTable: "CompetitionCompetitor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonRankingDetail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonRankingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Current = table.Column<int>(type: "integer", nullable: false),
                    Previous = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<double>(type: "double precision", nullable: false),
                    FirstPlaceVotes = table.Column<int>(type: "integer", nullable: false),
                    Trend = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonRankingDetail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonRankingDetail_FranchiseSeasonRanking_Franchi~",
                        column: x => x.FranchiseSeasonRankingId,
                        principalTable: "FranchiseSeasonRanking",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonRankingExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RankingId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_FranchiseSeasonRankingExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonRankingExternalId_FranchiseSeasonRanking_Ran~",
                        column: x => x.RankingId,
                        principalTable: "FranchiseSeasonRanking",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonRankingNote",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RankingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonRankingNote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonRankingNote_FranchiseSeasonRanking_RankingId",
                        column: x => x.RankingId,
                        principalTable: "FranchiseSeasonRanking",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonRankingOccurrence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RankingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Last = table.Column<bool>(type: "boolean", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    DisplayValue = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonRankingOccurrence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonRankingOccurrence_FranchiseSeasonRanking_Ran~",
                        column: x => x.RankingId,
                        principalTable: "FranchiseSeasonRanking",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthleteSeasonStatisticStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonStatisticCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    ShortDisplayName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Abbreviation = table.Column<string>(type: "text", nullable: false),
                    DisplayValue = table.Column<string>(type: "text", nullable: false),
                    PerGameDisplayValue = table.Column<string>(type: "text", nullable: true),
                    Value = table.Column<decimal>(type: "numeric", nullable: true),
                    PerGameValue = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteSeasonStatisticStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteSeasonStatisticStat_AthleteSeasonStatisticCategory_A~",
                        column: x => x.AthleteSeasonStatisticCategoryId,
                        principalTable: "AthleteSeasonStatisticCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionCompetitorLineScoreExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorLineScoreId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CompetitionCompetitorLineScoreExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorLineScoreExternalId_CompetitionCompeti~",
                        column: x => x.CompetitionCompetitorLineScoreId,
                        principalTable: "CompetitionCompetitorLineScore",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionCompetitorScoreExternalIds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorScoreId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CompetitionCompetitorScoreExternalIds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorScoreExternalIds_CompetitionCompetitor~",
                        column: x => x.CompetitionCompetitorScoreId,
                        principalTable: "CompetitionCompetitorScores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonRankingDetailRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonRankingDetailId = table.Column<Guid>(type: "uuid", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonRankingDetailRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonRankingDetailRecord_FranchiseSeasonRankingDe~",
                        column: x => x.FranchiseSeasonRankingDetailId,
                        principalTable: "FranchiseSeasonRankingDetail",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonRankingDetailRecordStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonRankingDetailRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    ShortDisplayName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Abbreviation = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    DisplayValue = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonRankingDetailRecordStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonRankingDetailRecordStat_FranchiseSeasonRanki~",
                        column: x => x.FranchiseSeasonRankingDetailRecordId,
                        principalTable: "FranchiseSeasonRankingDetailRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupSeason_GroupId1",
                table: "GroupSeason",
                column: "GroupId1");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteSeason_PositionId",
                table: "AthleteSeason",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteSeason_StatusId",
                table: "AthleteSeason",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteSeasonExternalId_AthleteSeasonId",
                table: "AthleteSeasonExternalId",
                column: "AthleteSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteSeasonStatistic_AthleteSeasonId",
                table: "AthleteSeasonStatistic",
                column: "AthleteSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteSeasonStatisticCategory_AthleteSeasonStatisticId",
                table: "AthleteSeasonStatisticCategory",
                column: "AthleteSeasonStatisticId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteSeasonStatisticStat_AthleteSeasonStatisticCategoryId",
                table: "AthleteSeasonStatisticStat",
                column: "AthleteSeasonStatisticCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitor_CompetitionId",
                table: "CompetitionCompetitor",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorExternalIds_CompetitionCompetitorId",
                table: "CompetitionCompetitorExternalIds",
                column: "CompetitionCompetitorId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorLineScore_CompetitionCompetitorId",
                table: "CompetitionCompetitorLineScore",
                column: "CompetitionCompetitorId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorLineScoreExternalId_CompetitionCompeti~",
                table: "CompetitionCompetitorLineScoreExternalId",
                column: "CompetitionCompetitorLineScoreId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorScoreExternalIds_CompetitionCompetitor~",
                table: "CompetitionCompetitorScoreExternalIds",
                column: "CompetitionCompetitorScoreId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorScores_CompetitionCompetitorId",
                table: "CompetitionCompetitorScores",
                column: "CompetitionCompetitorId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRanking_FranchiseId",
                table: "FranchiseSeasonRanking",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRanking_FranchiseSeasonId",
                table: "FranchiseSeasonRanking",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRankingDetail_FranchiseSeasonRankingId",
                table: "FranchiseSeasonRankingDetail",
                column: "FranchiseSeasonRankingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRankingDetailRecord_FranchiseSeasonRankingDe~",
                table: "FranchiseSeasonRankingDetailRecord",
                column: "FranchiseSeasonRankingDetailId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRankingDetailRecordStat_FranchiseSeasonRanki~",
                table: "FranchiseSeasonRankingDetailRecordStat",
                column: "FranchiseSeasonRankingDetailRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRankingExternalId_RankingId",
                table: "FranchiseSeasonRankingExternalId",
                column: "RankingId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRankingNote_RankingId",
                table: "FranchiseSeasonRankingNote",
                column: "RankingId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRankingOccurrence_RankingId",
                table: "FranchiseSeasonRankingOccurrence",
                column: "RankingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupSeasonExternalId_GroupSeasonId",
                table: "GroupSeasonExternalId",
                column: "GroupSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupSeasonExternalId_GroupSeasonId1",
                table: "GroupSeasonExternalId",
                column: "GroupSeasonId1");

            migrationBuilder.AddForeignKey(
                name: "FK_AthleteSeason_AthletePosition_PositionId",
                table: "AthleteSeason",
                column: "PositionId",
                principalTable: "AthletePosition",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AthleteSeason_AthleteStatus_StatusId",
                table: "AthleteSeason",
                column: "StatusId",
                principalTable: "AthleteStatus",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupSeason_Group_GroupId1",
                table: "GroupSeason",
                column: "GroupId1",
                principalTable: "Group",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AthleteSeason_AthletePosition_PositionId",
                table: "AthleteSeason");

            migrationBuilder.DropForeignKey(
                name: "FK_AthleteSeason_AthleteStatus_StatusId",
                table: "AthleteSeason");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupSeason_Group_GroupId1",
                table: "GroupSeason");

            migrationBuilder.DropTable(
                name: "AthleteSeasonExternalId");

            migrationBuilder.DropTable(
                name: "AthleteSeasonStatisticStat");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorExternalIds");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorLineScoreExternalId");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorScoreExternalIds");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRankingDetailRecordStat");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRankingExternalId");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRankingNote");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRankingOccurrence");

            migrationBuilder.DropTable(
                name: "GroupSeasonExternalId");

            migrationBuilder.DropTable(
                name: "AthleteSeasonStatisticCategory");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorLineScore");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorScores");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRankingDetailRecord");

            migrationBuilder.DropTable(
                name: "AthleteSeasonStatistic");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitor");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRankingDetail");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRanking");

            migrationBuilder.DropIndex(
                name: "IX_GroupSeason_GroupId1",
                table: "GroupSeason");

            migrationBuilder.DropIndex(
                name: "IX_AthleteSeason_PositionId",
                table: "AthleteSeason");

            migrationBuilder.DropIndex(
                name: "IX_AthleteSeason_StatusId",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "Abbreviation",
                table: "GroupSeason");

            migrationBuilder.DropColumn(
                name: "GroupId1",
                table: "GroupSeason");

            migrationBuilder.DropColumn(
                name: "MidsizeName",
                table: "GroupSeason");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "GroupSeason");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "GroupSeason");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "GroupSeason");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "ExperienceAbbreviation",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "ExperienceDisplayValue",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "ExperienceYears",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "HeightDisplay",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "HeightIn",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "Jersey",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "WeightDisplay",
                table: "AthleteSeason");

            migrationBuilder.DropColumn(
                name: "WeightLb",
                table: "AthleteSeason");

            migrationBuilder.CreateTable(
                name: "Competitor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CuratedRankCurrent = table.Column<int>(type: "integer", nullable: true),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeAway = table.Column<string>(type: "text", nullable: true),
                    LeadersRef = table.Column<string>(type: "text", nullable: true),
                    LinescoresRef = table.Column<string>(type: "text", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    RanksRef = table.Column<string>(type: "text", nullable: true),
                    RecordRef = table.Column<string>(type: "text", nullable: true),
                    RosterRef = table.Column<string>(type: "text", nullable: true),
                    ScoreDisplayValue = table.Column<string>(type: "text", nullable: false),
                    ScoreValue = table.Column<int>(type: "integer", nullable: false),
                    StatisticsRef = table.Column<string>(type: "text", nullable: true),
                    TeamRef = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: true),
                    Winner = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competitor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Competitor_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Competitor_CompetitionId",
                table: "Competitor",
                column: "CompetitionId");
        }
    }
}
