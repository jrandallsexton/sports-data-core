using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AthletePosition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Leaf = table.Column<bool>(type: "boolean", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthletePosition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthletePosition_AthletePosition_ParentId",
                        column: x => x.ParentId,
                        principalTable: "AthletePosition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Contests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HomeTeamFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayTeamFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Clock = table.Column<int>(type: "integer", nullable: false),
                    DisplayClock = table.Column<string>(type: "text", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonType = table.Column<int>(type: "integer", nullable: true),
                    Week = table.Column<int>(type: "integer", nullable: true),
                    NeutralSite = table.Column<bool>(type: "boolean", nullable: true),
                    Attendance = table.Column<int>(type: "integer", nullable: true),
                    EventNote = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Franchise",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Nickname = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DisplayNameShort = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ColorCodeHex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    ColorCodeAltHex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Franchise", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Group",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MidsizeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsConference = table.Column<bool>(type: "boolean", nullable: false),
                    ParentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Group", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxState",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceiveCount = table.Column<int>(type: "integer", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Consumed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxState", x => x.Id);
                    table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "SeasonYear",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Slug = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonYear", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Venue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsGrass = table.Column<bool>(type: "boolean", nullable: false),
                    IsIndoor = table.Column<bool>(type: "boolean", nullable: false),
                    Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    City = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Country = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric", nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Athlete",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WeightLb = table.Column<decimal>(type: "numeric", nullable: false),
                    WeightDisplay = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    HeightIn = table.Column<decimal>(type: "numeric", nullable: false),
                    HeightDisplay = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Age = table.Column<int>(type: "integer", nullable: false),
                    DoB = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentExperience = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Discriminator = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    FranchiseId = table.Column<Guid>(type: "uuid", nullable: true),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentPosition = table.Column<Guid>(type: "uuid", nullable: true),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Athlete", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Athlete_AthletePosition_PositionId",
                        column: x => x.PositionId,
                        principalTable: "AthletePosition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthletePositionExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthletePositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthletePositionExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthletePositionExternalId_AthletePosition_AthletePositionId",
                        column: x => x.AthletePositionId,
                        principalTable: "AthletePosition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContestExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContestExternalId_Contests_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContestLink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Rel = table.Column<string>(type: "text", nullable: false),
                    Href = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: true),
                    ShortText = table.Column<string>(type: "text", nullable: true),
                    IsExternal = table.Column<bool>(type: "boolean", nullable: false),
                    IsPremium = table.Column<bool>(type: "boolean", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContestLink_Contests_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContestOdds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderRef = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderPriority = table.Column<int>(type: "integer", nullable: false),
                    Details = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OverUnder = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Spread = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    OverOdds = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    UnderOdds = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    MoneylineWinner = table.Column<bool>(type: "boolean", nullable: false),
                    SpreadWinner = table.Column<bool>(type: "boolean", nullable: false),
                    AwayTeamFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    AwayTeamUnderdog = table.Column<bool>(type: "boolean", nullable: false),
                    AwayTeamMoneyLine = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamSpreadOdds = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AwayTeamOpenFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    AwayTeamOpenPointSpreadAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamOpenPointSpreadAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamOpenSpreadValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AwayTeamOpenSpreadDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamOpenSpreadAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamOpenSpreadDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AwayTeamOpenSpreadFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamOpenSpreadAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamOpenMoneyLineValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AwayTeamOpenMoneyLineDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamOpenMoneyLineAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamOpenMoneyLineDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AwayTeamOpenMoneyLineFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamOpenMoneyLineAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamClosePointSpreadAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamClosePointSpreadAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCloseSpreadValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AwayTeamCloseSpreadDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCloseSpreadAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCloseSpreadDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AwayTeamCloseSpreadFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCloseSpreadAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCloseMoneyLineValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AwayTeamCloseMoneyLineDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCloseMoneyLineAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCloseMoneyLineDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AwayTeamCloseMoneyLineFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCloseMoneyLineAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCurrentPointSpreadAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCurrentPointSpreadAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCurrentSpreadValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AwayTeamCurrentSpreadDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCurrentSpreadAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCurrentSpreadDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AwayTeamCurrentSpreadFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCurrentSpreadAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCurrentSpreadOutcomeType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCurrentMoneyLineValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AwayTeamCurrentMoneyLineDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCurrentMoneyLineAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCurrentMoneyLineDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AwayTeamCurrentMoneyLineFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCurrentMoneyLineAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamCurrentMoneyLineOutcomeType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayTeamFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeTeamFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    HomeTeamUnderdog = table.Column<bool>(type: "boolean", nullable: false),
                    HomeTeamMoneyLine = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamSpreadOdds = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamOpenFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    HomeTeamOpenPointSpreadValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamOpenPointSpreadDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamOpenPointSpreadAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamOpenPointSpreadDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamOpenPointSpreadFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamOpenPointSpreadAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamOpenSpreadValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamOpenSpreadDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamOpenSpreadAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamOpenSpreadDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamOpenSpreadFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamOpenSpreadAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamOpenMoneyLineValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamOpenMoneyLineDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamOpenMoneyLineAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamOpenMoneyLineDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamOpenMoneyLineFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamOpenMoneyLineAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamClosePointSpreadAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamClosePointSpreadAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCloseSpreadValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamCloseSpreadDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCloseSpreadAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCloseSpreadDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamCloseSpreadFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCloseSpreadAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCloseMoneyLineValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamCloseMoneyLineDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCloseMoneyLineAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCloseMoneyLineDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamCloseMoneyLineFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCloseMoneyLineAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCurrentPointSpreadAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCurrentPointSpreadAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCurrentSpreadValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamCurrentSpreadDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCurrentSpreadAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCurrentSpreadDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamCurrentSpreadFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCurrentSpreadAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCurrentSpreadOutcomeType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCurrentMoneyLineValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamCurrentMoneyLineDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCurrentMoneyLineAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCurrentMoneyLineDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    HomeTeamCurrentMoneyLineFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCurrentMoneyLineAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamCurrentMoneyLineOutcomeType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HomeTeamFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenOverValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    OpenOverDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OpenOverAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OpenOverDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    OpenOverFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OpenOverAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OpenUnderValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    OpenUnderDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OpenUnderAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OpenUnderDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    OpenUnderFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OpenUnderAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OpenTotalValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    OpenTotalDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OpenTotalAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OpenTotalDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    OpenTotalFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OpenTotalAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CloseOverValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CloseOverDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CloseOverAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CloseOverDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CloseOverFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CloseOverAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CloseUnderValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CloseUnderDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CloseUnderAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CloseUnderDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CloseUnderFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CloseUnderAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CloseTotalAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CloseTotalAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CloseTotalValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CloseTotalDisplayValue = table.Column<string>(type: "text", nullable: true),
                    CloseTotalDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CloseTotalFraction = table.Column<string>(type: "text", nullable: true),
                    CurrentOverValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CurrentOverDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrentOverAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrentOverDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CurrentOverFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrentOverAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrentOverOutcomeType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrentUnderValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CurrentUnderDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrentUnderAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrentUnderDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CurrentUnderFraction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrentUnderAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrentUnderOutcomeType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrentTotalAlternateDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrentTotalAmerican = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrentTotalValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CurrentTotalDisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrentTotalDecimal = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CurrentTotalFraction = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestOdds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContestOdds_Contests_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseExternalId_Franchise_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "Franchise",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseLogo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalUrlHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Uri = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Height = table.Column<long>(type: "bigint", nullable: true),
                    Width = table.Column<long>(type: "bigint", nullable: true),
                    Rel = table.Column<List<string>>(type: "text[]", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseLogo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseLogo_Franchise_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "Franchise",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupExternalId_Group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupLogo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalUrlHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Uri = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Height = table.Column<long>(type: "bigint", nullable: true),
                    Width = table.Column<long>(type: "bigint", nullable: true),
                    Rel = table.Column<List<string>>(type: "text[]", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupLogo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupLogo_Group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupSeason",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupSeason", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupSeason_Group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnqueueTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Headers = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    InboxConsumerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DestinationAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResponseAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FaultAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                        columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateTable(
                name: "SeasonExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonExternalId_SeasonYear_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "SeasonYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeason",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayNameShort = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ColorCodeHex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    ColorCodeAltHex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsAllStar = table.Column<bool>(type: "boolean", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    Losses = table.Column<int>(type: "integer", nullable: false),
                    Ties = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeason", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeason_Franchise_FranchiseId",
                        column: x => x.FranchiseId,
                        principalTable: "Franchise",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FranchiseSeason_Group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FranchiseSeason_Venue_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VenueExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VenueExternalId_Venue_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VenueImage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalUrlHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Uri = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Height = table.Column<long>(type: "bigint", nullable: true),
                    Width = table.Column<long>(type: "bigint", nullable: true),
                    Rel = table.Column<List<string>>(type: "text[]", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueImage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VenueImage_Venue_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthleteExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteExternalId_Athlete_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athlete",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthleteImage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalUrlHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Uri = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Height = table.Column<long>(type: "bigint", nullable: true),
                    Width = table.Column<long>(type: "bigint", nullable: true),
                    Rel = table.Column<List<string>>(type: "text[]", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteImage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteImage_Athlete_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athlete",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthleteSeason",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteSeason", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteSeason_Athlete_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athlete",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupSeasonLogo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalUrlHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Uri = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Height = table.Column<long>(type: "bigint", nullable: true),
                    Width = table.Column<long>(type: "bigint", nullable: true),
                    Rel = table.Column<List<string>>(type: "text[]", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupSeasonLogo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupSeasonLogo_GroupSeason_GroupSeasonId",
                        column: x => x.GroupSeasonId,
                        principalTable: "GroupSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonExternalId_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonLogo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalUrlHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Uri = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Height = table.Column<long>(type: "bigint", nullable: true),
                    Width = table.Column<long>(type: "bigint", nullable: true),
                    Rel = table.Column<List<string>>(type: "text[]", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonLogo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonLogo_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Summary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DisplayValue = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonRecord_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonRecordStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    DisplayValue = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonRecordStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonRecordStat_FranchiseSeasonRecord_FranchiseSe~",
                        column: x => x.FranchiseSeasonRecordId,
                        principalTable: "FranchiseSeasonRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Athlete_PositionId",
                table: "Athlete",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteExternalId_AthleteId",
                table: "AthleteExternalId",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteImage_AthleteId",
                table: "AthleteImage",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteImage_OriginalUrlHash",
                table: "AthleteImage",
                column: "OriginalUrlHash");

            migrationBuilder.CreateIndex(
                name: "IX_AthletePosition_ParentId",
                table: "AthletePosition",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_AthletePositionExternalId_AthletePositionId",
                table: "AthletePositionExternalId",
                column: "AthletePositionId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteSeason_AthleteId",
                table: "AthleteSeason",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_ContestExternalId_ContestId",
                table: "ContestExternalId",
                column: "ContestId");

            migrationBuilder.CreateIndex(
                name: "IX_ContestLink_ContestId",
                table: "ContestLink",
                column: "ContestId");

            migrationBuilder.CreateIndex(
                name: "IX_ContestOdds_ContestId",
                table: "ContestOdds",
                column: "ContestId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseExternalId_FranchiseId",
                table: "FranchiseExternalId",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseLogo_FranchiseId",
                table: "FranchiseLogo",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseLogo_OriginalUrlHash",
                table: "FranchiseLogo",
                column: "OriginalUrlHash");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeason_FranchiseId",
                table: "FranchiseSeason",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeason_GroupId",
                table: "FranchiseSeason",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeason_VenueId",
                table: "FranchiseSeason",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonExternalId_FranchiseSeasonId",
                table: "FranchiseSeasonExternalId",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonLogo_FranchiseSeasonId",
                table: "FranchiseSeasonLogo",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonLogo_OriginalUrlHash",
                table: "FranchiseSeasonLogo",
                column: "OriginalUrlHash");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRecord_FranchiseSeasonId",
                table: "FranchiseSeasonRecord",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRecordStat_FranchiseSeasonRecordId",
                table: "FranchiseSeasonRecordStat",
                column: "FranchiseSeasonRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupExternalId_GroupId",
                table: "GroupExternalId",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupLogo_GroupId",
                table: "GroupLogo",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupLogo_OriginalUrlHash",
                table: "GroupLogo",
                column: "OriginalUrlHash");

            migrationBuilder.CreateIndex(
                name: "IX_GroupSeason_GroupId",
                table: "GroupSeason",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupSeasonLogo_GroupSeasonId",
                table: "GroupSeasonLogo",
                column: "GroupSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupSeasonLogo_OriginalUrlHash",
                table: "GroupSeasonLogo",
                column: "OriginalUrlHash");

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonExternalId_SeasonId",
                table: "SeasonExternalId",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueExternalId_VenueId",
                table: "VenueExternalId",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueImage_OriginalUrlHash",
                table: "VenueImage",
                column: "OriginalUrlHash");

            migrationBuilder.CreateIndex(
                name: "IX_VenueImage_VenueId",
                table: "VenueImage",
                column: "VenueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AthleteExternalId");

            migrationBuilder.DropTable(
                name: "AthleteImage");

            migrationBuilder.DropTable(
                name: "AthletePositionExternalId");

            migrationBuilder.DropTable(
                name: "AthleteSeason");

            migrationBuilder.DropTable(
                name: "ContestExternalId");

            migrationBuilder.DropTable(
                name: "ContestLink");

            migrationBuilder.DropTable(
                name: "ContestOdds");

            migrationBuilder.DropTable(
                name: "FranchiseExternalId");

            migrationBuilder.DropTable(
                name: "FranchiseLogo");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonExternalId");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonLogo");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRecordStat");

            migrationBuilder.DropTable(
                name: "GroupExternalId");

            migrationBuilder.DropTable(
                name: "GroupLogo");

            migrationBuilder.DropTable(
                name: "GroupSeasonLogo");

            migrationBuilder.DropTable(
                name: "OutboxMessage");

            migrationBuilder.DropTable(
                name: "SeasonExternalId");

            migrationBuilder.DropTable(
                name: "VenueExternalId");

            migrationBuilder.DropTable(
                name: "VenueImage");

            migrationBuilder.DropTable(
                name: "Athlete");

            migrationBuilder.DropTable(
                name: "Contests");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRecord");

            migrationBuilder.DropTable(
                name: "GroupSeason");

            migrationBuilder.DropTable(
                name: "InboxState");

            migrationBuilder.DropTable(
                name: "OutboxState");

            migrationBuilder.DropTable(
                name: "SeasonYear");

            migrationBuilder.DropTable(
                name: "AthletePosition");

            migrationBuilder.DropTable(
                name: "FranchiseSeason");

            migrationBuilder.DropTable(
                name: "Franchise");

            migrationBuilder.DropTable(
                name: "Group");

            migrationBuilder.DropTable(
                name: "Venue");
        }
    }
}
