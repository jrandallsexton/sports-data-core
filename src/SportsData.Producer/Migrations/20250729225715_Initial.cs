using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

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
                name: "AthleteStatus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Award",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    History = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Award", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Coach",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Experience = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coach", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionPrediction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsHome = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionPrediction", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionPredictionValue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionPredictionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionMetricId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    DisplayValue = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionPredictionValue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionSource",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionSource", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Franchise",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Nickname = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Abbreviation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
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
                name: "lkLeaderCategory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(75)", maxLength: 75, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lkLeaderCategory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lkPlayType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(75)", maxLength: 75, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lkPlayType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lkRecordAtsCategory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(75)", maxLength: 75, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lkRecordAtsCategory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Location",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    City = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    State = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Country = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Location", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxPings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxPings", x => x.Id);
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
                name: "PowerIndex",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(75)", maxLength: 75, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PowerIndex", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PredictionMetric",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionMetric", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Venue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(75)", maxLength: 75, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(75)", maxLength: 75, nullable: true),
                    IsGrass = table.Column<bool>(type: "boolean", nullable: false),
                    IsIndoor = table.Column<bool>(type: "boolean", nullable: false),
                    Slug = table.Column<string>(type: "character varying(75)", maxLength: 75, nullable: false),
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
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
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
                name: "AwardExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AwardId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AwardExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwardExternalId_Award_AwardId",
                        column: x => x.AwardId,
                        principalTable: "Award",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoachExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CoachExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachExternalId_Coach_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coach",
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
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
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
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
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
                name: "Athlete",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WeightLb = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    WeightDisplay = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    HeightIn = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    HeightDisplay = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Age = table.Column<int>(type: "integer", nullable: false),
                    DoB = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BirthLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExperienceAbbreviation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ExperienceDisplayValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ExperienceYears = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StatusId = table.Column<Guid>(type: "uuid", nullable: true),
                    Discriminator = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    FranchiseId = table.Column<Guid>(type: "uuid", nullable: true),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
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
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Athlete_AthleteStatus_StatusId",
                        column: x => x.StatusId,
                        principalTable: "AthleteStatus",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Athlete_Location_BirthLocationId",
                        column: x => x.BirthLocationId,
                        principalTable: "Location",
                        principalColumn: "Id");
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
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
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
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
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
                name: "CoachSeason",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachSeason", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachSeason_Coach_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coach",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CoachSeason_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Contest",
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
                    Clock = table.Column<double>(type: "double precision", nullable: false),
                    DisplayClock = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonType = table.Column<int>(type: "integer", nullable: true),
                    Week = table.Column<int>(type: "integer", nullable: true),
                    EventNote = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contest_FranchiseSeason_AwayTeamFranchiseSeasonId",
                        column: x => x.AwayTeamFranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contest_FranchiseSeason_HomeTeamFranchiseSeasonId",
                        column: x => x.HomeTeamFranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contest_Venue_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonAward",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwardId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonAward", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonAward_Award_AwardId",
                        column: x => x.AwardId,
                        principalTable: "Award",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonAward_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
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
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
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
                name: "FranchiseSeasonProjection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    ChanceToWinDivision = table.Column<decimal>(type: "numeric", nullable: false),
                    ChanceToWinConference = table.Column<decimal>(type: "numeric", nullable: false),
                    ProjectedWins = table.Column<decimal>(type: "numeric", nullable: false),
                    ProjectedLosses = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonProjection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonProjection_FranchiseSeason_FranchiseSeasonId",
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
                name: "FranchiseSeasonRecordAts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: true),
                    Losses = table.Column<int>(type: "integer", nullable: true),
                    Pushes = table.Column<int>(type: "integer", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonRecordAts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonRecordAts_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonRecordAts_lkRecordAtsCategory_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "lkRecordAtsCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonStatisticCategory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonStatisticCategory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonStatisticCategory_FranchiseSeason_FranchiseS~",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Competition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attendance = table.Column<int>(type: "integer", nullable: false),
                    TimeValid = table.Column<bool>(type: "boolean", nullable: false),
                    DateValid = table.Column<bool>(type: "boolean", nullable: false),
                    IsNeutralSite = table.Column<bool>(type: "boolean", nullable: false),
                    IsDivisionCompetition = table.Column<bool>(type: "boolean", nullable: false),
                    IsConferenceCompetition = table.Column<bool>(type: "boolean", nullable: false),
                    IsPreviewAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsRecapAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsBoxscoreAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsLineupAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsGamecastAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlayByPlayAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsConversationAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsCommentaryAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsPickCenterAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsSummaryAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsLiveAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsTicketsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsShotChartAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsTimeoutsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsPossessionArrowAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsOnWatchEspn = table.Column<bool>(type: "boolean", nullable: false),
                    IsRecent = table.Column<bool>(type: "boolean", nullable: false),
                    IsBracketAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsWallClockAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsHighlightsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    HasDefensiveStats = table.Column<bool>(type: "boolean", nullable: false),
                    TypeId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TypeText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TypeAbbreviation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TypeSlug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TypeName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GameSourceId = table.Column<int>(type: "integer", nullable: true),
                    BoxscoreSourceId = table.Column<int>(type: "integer", nullable: true),
                    LinescoreSourceId = table.Column<int>(type: "integer", nullable: true),
                    PlayByPlaySourceId = table.Column<int>(type: "integer", nullable: true),
                    StatsSourceId = table.Column<int>(type: "integer", nullable: true),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: true),
                    FormatRegulationDisplayName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FormatRegulationPeriods = table.Column<int>(type: "integer", nullable: true),
                    FormatRegulationSlug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FormatRegulationClock = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: true),
                    FormatOvertimeDisplayName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FormatOvertimePeriods = table.Column<int>(type: "integer", nullable: true),
                    FormatOvertimeSlug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Competition_CompetitionSource_BoxscoreSourceId",
                        column: x => x.BoxscoreSourceId,
                        principalTable: "CompetitionSource",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Competition_CompetitionSource_GameSourceId",
                        column: x => x.GameSourceId,
                        principalTable: "CompetitionSource",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Competition_CompetitionSource_LinescoreSourceId",
                        column: x => x.LinescoreSourceId,
                        principalTable: "CompetitionSource",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Competition_CompetitionSource_PlayByPlaySourceId",
                        column: x => x.PlayByPlaySourceId,
                        principalTable: "CompetitionSource",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Competition_CompetitionSource_StatsSourceId",
                        column: x => x.StatsSourceId,
                        principalTable: "CompetitionSource",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Competition_Contest_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Competition_Venue_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContestExternalId_Contest_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contest",
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
                        name: "FK_ContestLink_Contest_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contest",
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
                        name: "FK_ContestOdds_Contest_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonAwardWinner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonAwardId = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TeamRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonAwardWinner", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonAwardWinner_FranchiseSeasonAward_FranchiseSe~",
                        column: x => x.FranchiseSeasonAwardId,
                        principalTable: "FranchiseSeasonAward",
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

            migrationBuilder.CreateTable(
                name: "FranchiseSeasonStatistic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonStatisticCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    DisplayValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    RankDisplayValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PerGameValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PerGameDisplayValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonStatistic", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonStatistic_FranchiseSeasonStatisticCategory_F~",
                        column: x => x.FranchiseSeasonStatisticCategoryId,
                        principalTable: "FranchiseSeasonStatisticCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Broadcast",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TypeShortName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TypeLongName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TypeSlug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Station = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StationKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Url = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    MarketId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    MarketType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MediaId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    MediaCallLetters = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MediaName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MediaShortName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MediaSlug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MediaGroupId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    MediaGroupName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MediaGroupSlug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Region = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Partnered = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Broadcast", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Broadcast_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CompetitionExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionExternalId_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionLeader",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaderCategoryId = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionLeader", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionLeader_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionLeader_lkLeaderCategory_LeaderCategoryId",
                        column: x => x.LeaderCategoryId,
                        principalTable: "lkLeaderCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionLink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Rel = table.Column<string>(type: "text", nullable: false),
                    Href = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: true),
                    ShortText = table.Column<string>(type: "text", nullable: true),
                    IsExternal = table.Column<bool>(type: "boolean", nullable: false),
                    IsPremium = table.Column<bool>(type: "boolean", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionLink_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionNote",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Headline = table.Column<string>(type: "text", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionNote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionNote_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionPowerIndex",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PowerIndexId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<double>(type: "double precision", precision: 18, scale: 6, nullable: false),
                    DisplayValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionPowerIndex", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionPowerIndex_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionPowerIndex_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionPowerIndex_PowerIndex_PowerIndexId",
                        column: x => x.PowerIndexId,
                        principalTable: "PowerIndex",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionStatus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Clock = table.Column<double>(type: "double precision", nullable: false),
                    DisplayClock = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    StatusTypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StatusTypeName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StatusState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    StatusDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StatusDetail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StatusShortDetail = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionStatus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionStatus_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Competitor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    HomeAway = table.Column<string>(type: "text", nullable: true),
                    Winner = table.Column<bool>(type: "boolean", nullable: false),
                    TeamRef = table.Column<string>(type: "text", nullable: true),
                    ScoreValue = table.Column<int>(type: "integer", nullable: false),
                    ScoreDisplayValue = table.Column<string>(type: "text", nullable: false),
                    LinescoresRef = table.Column<string>(type: "text", nullable: true),
                    RosterRef = table.Column<string>(type: "text", nullable: true),
                    StatisticsRef = table.Column<string>(type: "text", nullable: true),
                    LeadersRef = table.Column<string>(type: "text", nullable: true),
                    RecordRef = table.Column<string>(type: "text", nullable: true),
                    RanksRef = table.Column<string>(type: "text", nullable: true),
                    CuratedRankCurrent = table.Column<int>(type: "integer", nullable: true),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "Drive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    SequenceNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ShortDisplayResult = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayResult = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Yards = table.Column<int>(type: "integer", nullable: false),
                    OffensivePlays = table.Column<int>(type: "integer", nullable: false),
                    IsScore = table.Column<bool>(type: "boolean", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SourceDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartPeriodType = table.Column<string>(type: "text", nullable: true),
                    StartPeriodNumber = table.Column<int>(type: "integer", nullable: true),
                    StartClockValue = table.Column<double>(type: "double precision", nullable: true),
                    StartClockDisplayValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StartYardLine = table.Column<int>(type: "integer", nullable: true),
                    StartText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartDown = table.Column<int>(type: "integer", nullable: true),
                    StartDistance = table.Column<int>(type: "integer", nullable: true),
                    StartYardsToEndzone = table.Column<int>(type: "integer", nullable: true),
                    StartFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartDownDistanceText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StartShortDownDistanceText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EndPeriodType = table.Column<string>(type: "text", nullable: true),
                    EndPeriodNumber = table.Column<int>(type: "integer", nullable: true),
                    EndClockValue = table.Column<double>(type: "double precision", nullable: true),
                    EndClockDisplayValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EndYardLine = table.Column<int>(type: "integer", nullable: true),
                    EndText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EndDown = table.Column<int>(type: "integer", nullable: true),
                    EndDistance = table.Column<int>(type: "integer", nullable: true),
                    EndYardsToEndzone = table.Column<int>(type: "integer", nullable: true),
                    EndFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    EndDownDistanceText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EndShortDownDistanceText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TimeElapsedDisplay = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TimeElapsedValue = table.Column<double>(type: "double precision", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drive", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Drive_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionLeaderStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionLeaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<double>(type: "double precision", precision: 18, scale: 6, nullable: false),
                    AthleteId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionLeaderStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionLeaderStat_Athlete_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athlete",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionLeaderStat_CompetitionLeader_CompetitionLeaderId",
                        column: x => x.CompetitionLeaderId,
                        principalTable: "CompetitionLeader",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionLeaderStat_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionPowerIndexExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionPowerIndexId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CompetitionPowerIndexExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionPowerIndexExternalId_CompetitionPowerIndex_Compe~",
                        column: x => x.CompetitionPowerIndexId,
                        principalTable: "CompetitionPowerIndex",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionStatusExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionStatusId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CompetitionStatusExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionStatusExternalId_CompetitionStatus_CompetitionSt~",
                        column: x => x.CompetitionStatusId,
                        principalTable: "CompetitionStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DriveExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriveId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_DriveExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriveExternalId_Drive_DriveId",
                        column: x => x.DriveId,
                        principalTable: "Drive",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Play",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriveId = table.Column<Guid>(type: "uuid", nullable: true),
                    EspnId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SequenceNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    TypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ShortText = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    AlternativeText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ShortAlternativeText = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    AwayScore = table.Column<int>(type: "integer", nullable: false),
                    HomeScore = table.Column<int>(type: "integer", nullable: false),
                    PeriodNumber = table.Column<int>(type: "integer", nullable: false),
                    ClockValue = table.Column<double>(type: "double precision", nullable: false),
                    ClockDisplayValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ScoringPlay = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<bool>(type: "boolean", nullable: false),
                    ScoreValue = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TeamFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDown = table.Column<int>(type: "integer", nullable: true),
                    StartDistance = table.Column<int>(type: "integer", nullable: true),
                    StartYardLine = table.Column<int>(type: "integer", nullable: true),
                    StartYardsToEndzone = table.Column<int>(type: "integer", nullable: true),
                    StartTeamFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    EndDown = table.Column<int>(type: "integer", nullable: true),
                    EndDistance = table.Column<int>(type: "integer", nullable: true),
                    EndYardLine = table.Column<int>(type: "integer", nullable: true),
                    EndYardsToEndzone = table.Column<int>(type: "integer", nullable: true),
                    StatYardage = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Play", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Play_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Play_Drive_DriveId",
                        column: x => x.DriveId,
                        principalTable: "Drive",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionProbability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayId = table.Column<Guid>(type: "uuid", nullable: true),
                    TiePercentage = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    HomeWinPercentage = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    AwayWinPercentage = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    SecondsLeft = table.Column<int>(type: "integer", nullable: false),
                    LastModifiedRaw = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SequenceNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceDescription = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionProbability", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionProbability_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionProbability_Play_PlayId",
                        column: x => x.PlayId,
                        principalTable: "Play",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_PlayExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayExternalId_Play_PlayId",
                        column: x => x.PlayId,
                        principalTable: "Play",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionProbabilityExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionProbabilityId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CompetitionProbabilityExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionProbabilityExternalId_CompetitionProbability_Com~",
                        column: x => x.CompetitionProbabilityId,
                        principalTable: "CompetitionProbability",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Season",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActivePhaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Season", x => x.Id);
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
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonExternalId_Season_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Season",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonFuture",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonFuture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonFuture_Season_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Season",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeCode = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HasGroups = table.Column<bool>(type: "boolean", nullable: false),
                    HasStandings = table.Column<bool>(type: "boolean", nullable: false),
                    HasLegs = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonPhase", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonPhase_Season_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Season",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonFutureExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonFutureId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonFutureExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonFutureExternalId_SeasonFuture_SeasonFutureId",
                        column: x => x.SeasonFutureId,
                        principalTable: "SeasonFuture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonFutureItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonFutureId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: false),
                    ProviderName = table.Column<string>(type: "text", nullable: false),
                    ProviderActive = table.Column<int>(type: "integer", nullable: false),
                    ProviderPriority = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonFutureItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonFutureItem_SeasonFuture_SeasonFutureId",
                        column: x => x.SeasonFutureId,
                        principalTable: "SeasonFuture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonPhaseExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonPhaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonPhaseExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonPhaseExternalId_SeasonPhase_SeasonPhaseId",
                        column: x => x.SeasonPhaseId,
                        principalTable: "SeasonPhase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonFutureBook",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonFutureItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonFutureBook", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonFutureBook_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeasonFutureBook_SeasonFutureItem_SeasonFutureItemId",
                        column: x => x.SeasonFutureItemId,
                        principalTable: "SeasonFutureItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "lkLeaderCategory",
                columns: new[] { "Id", "Abbreviation", "CreatedBy", "CreatedUtc", "DisplayName", "ModifiedBy", "ModifiedUtc", "Name", "ShortDisplayName" },
                values: new object[,]
                {
                    { 1, "PYDS", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Passing Leader", null, null, "passingLeader", "PASS" },
                    { 2, "RYDS", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Rushing Leader", null, null, "rushingLeader", "RUSH" },
                    { 3, "RECYDS", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Receiving Leader", null, null, "receivingLeader", "REC" },
                    { 4, "YDS", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Passing Yards", null, null, "passingYards", "PYDS" },
                    { 5, "YDS", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Rushing Yards", null, null, "rushingYards", "RYDS" },
                    { 6, "YDS", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Receiving Yards", null, null, "receivingYards", "RECYDS" },
                    { 7, "TOT", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Tackles", null, null, "totalTackles", "TACK" },
                    { 8, "SACK", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sacks", null, null, "sacks", "SACK" },
                    { 9, "INT", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Interceptions", null, null, "interceptions", "INT" },
                    { 10, "PR", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Punt Returns", null, null, "puntReturns", "PR" },
                    { 11, "KR", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Kick Returns", null, null, "kickReturns", "KR" },
                    { 12, "P", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Punts", null, null, "punts", "P" },
                    { 13, "TP", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Kicking Points", null, null, "totalKickingPoints", "TP" },
                    { 14, "F", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Fumbles", null, null, "fumbles", "F" },
                    { 15, "FL", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Fumbles Lost", null, null, "fumblesLost", "FL" },
                    { 16, "CMP", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Fumbles Recovered", null, null, "fumblesRecovered", "CMP" },
                    { 17, "ESPNRating", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ESPN Rating Leader", null, null, "espnRating", "ESPNRating" },
                    { 18, "TD", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Passing Touchdowns", null, null, "passingTouchdowns", "TD" },
                    { 19, "RAT", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Quarterback Rating", null, null, "quarterbackRating", "RAT" },
                    { 20, "TD", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Rushing Touchdowns", null, null, "rushingTouchdowns", "TD" },
                    { 21, "REC", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Receptions", null, null, "receptions", "REC" },
                    { 22, "TD", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Receiving Touchdowns", null, null, "receivingTouchdowns", "TD" }
                });

            migrationBuilder.InsertData(
                table: "lkPlayType",
                columns: new[] { "Id", "CreatedBy", "CreatedUtc", "Description", "ModifiedBy", "ModifiedUtc", "Name" },
                values: new object[,]
                {
                    { 2, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "End Period", null, null, "endPeriod" },
                    { 3, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Pass Incompletion", null, null, "passIncompletion" },
                    { 5, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Rush", null, null, "rush" },
                    { 7, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sack", null, null, "sack" },
                    { 8, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Penalty", null, null, "penalty" },
                    { 9, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Fumble Recovery (Own)", null, null, "fumbleRecoveryOwn" },
                    { 12, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Kickoff Return (Offense)", null, null, "kickoffReturnOffense" },
                    { 21, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Timeout", null, null, "timeout" },
                    { 24, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Pass Reception", null, null, "passReception" },
                    { 26, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Pass Interception Return", null, null, "passInterceptionReturn" },
                    { 52, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Punt", null, null, "punt" },
                    { 53, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Kickoff", null, null, "kickoff" },
                    { 59, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Field Goal Good", null, null, "fieldGoalGood" },
                    { 60, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Field Goal Missed", null, null, "fieldGoalMissed" },
                    { 65, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "End of Half", null, null, "endOfHalf" },
                    { 66, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "End of Game", null, null, "endOfGame" },
                    { 67, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Passing Touchdown", null, null, "passingTouchdown" },
                    { 68, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Rushing Touchdown", null, null, "rushingTouchdown" },
                    { 70, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Coin Toss", null, null, "coinToss" }
                });

            migrationBuilder.InsertData(
                table: "lkRecordAtsCategory",
                columns: new[] { "Id", "CreatedBy", "CreatedUtc", "Description", "ModifiedBy", "ModifiedUtc", "Name" },
                values: new object[,]
                {
                    { 1, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Overall team season record against the spread", null, null, "atsOverall" },
                    { 2, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Team season record against the spread as the favorite", null, null, "atsFavorite" },
                    { 3, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Team season record against the spread as the underdog", null, null, "atsUnderdog" },
                    { 4, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Team season record against the spread as the away team", null, null, "atsAway" },
                    { 5, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Team season record against the spread as the home team", null, null, "atsHome" },
                    { 6, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Team season record against the spread as the away favorite", null, null, "atsAwayFavorite" },
                    { 7, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Team season record against the spread as the away underdog", null, null, "atsAwayUnderdog" },
                    { 8, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Team season record against the spread as the home favorite", null, null, "atsHomeFavorite" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Athlete_BirthLocationId",
                table: "Athlete",
                column: "BirthLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Athlete_PositionId",
                table: "Athlete",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Athlete_StatusId",
                table: "Athlete",
                column: "StatusId");

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
                name: "IX_AwardExternalId_AwardId",
                table: "AwardExternalId",
                column: "AwardId");

            migrationBuilder.CreateIndex(
                name: "IX_Broadcast_CompetitionId",
                table: "Broadcast",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachExternalId_CoachId",
                table: "CoachExternalId",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachSeason_CoachId",
                table: "CoachSeason",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachSeason_FranchiseSeasonId",
                table: "CoachSeason",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Competition_BoxscoreSourceId",
                table: "Competition",
                column: "BoxscoreSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Competition_ContestId",
                table: "Competition",
                column: "ContestId");

            migrationBuilder.CreateIndex(
                name: "IX_Competition_GameSourceId",
                table: "Competition",
                column: "GameSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Competition_LinescoreSourceId",
                table: "Competition",
                column: "LinescoreSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Competition_PlayByPlaySourceId",
                table: "Competition",
                column: "PlayByPlaySourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Competition_StatsSourceId",
                table: "Competition",
                column: "StatsSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Competition_VenueId",
                table: "Competition",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionExternalId_CompetitionId",
                table: "CompetitionExternalId",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionLeader_CompetitionId",
                table: "CompetitionLeader",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionLeader_LeaderCategoryId",
                table: "CompetitionLeader",
                column: "LeaderCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionLeaderStat_AthleteId",
                table: "CompetitionLeaderStat",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionLeaderStat_CompetitionLeaderId",
                table: "CompetitionLeaderStat",
                column: "CompetitionLeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionLeaderStat_FranchiseSeasonId",
                table: "CompetitionLeaderStat",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionLink_CompetitionId",
                table: "CompetitionLink",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionNote_CompetitionId",
                table: "CompetitionNote",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPowerIndex_CompetitionId",
                table: "CompetitionPowerIndex",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPowerIndex_FranchiseSeasonId",
                table: "CompetitionPowerIndex",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPowerIndex_PowerIndexId",
                table: "CompetitionPowerIndex",
                column: "PowerIndexId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPowerIndexExternalId_CompetitionPowerIndexId",
                table: "CompetitionPowerIndexExternalId",
                column: "CompetitionPowerIndexId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPrediction_CompetitionId_FranchiseSeasonId_IsHome",
                table: "CompetitionPrediction",
                columns: new[] { "CompetitionId", "FranchiseSeasonId", "IsHome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPredictionValue_CompetitionPredictionId_Predicti~",
                table: "CompetitionPredictionValue",
                columns: new[] { "CompetitionPredictionId", "PredictionMetricId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionProbability_CompetitionId",
                table: "CompetitionProbability",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionProbability_PlayId",
                table: "CompetitionProbability",
                column: "PlayId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionProbabilityExternalId_CompetitionProbabilityId",
                table: "CompetitionProbabilityExternalId",
                column: "CompetitionProbabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionStatus_CompetitionId",
                table: "CompetitionStatus",
                column: "CompetitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionStatusExternalId_CompetitionStatusId",
                table: "CompetitionStatusExternalId",
                column: "CompetitionStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Competitor_CompetitionId",
                table: "Competitor",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Contest_AwayTeamFranchiseSeasonId",
                table: "Contest",
                column: "AwayTeamFranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Contest_HomeTeamFranchiseSeasonId",
                table: "Contest",
                column: "HomeTeamFranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Contest_VenueId",
                table: "Contest",
                column: "VenueId");

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
                name: "IX_Drive_CompetitionId",
                table: "Drive",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_DriveExternalId_DriveId",
                table: "DriveExternalId",
                column: "DriveId");

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
                name: "IX_FranchiseSeasonAward_AwardId",
                table: "FranchiseSeasonAward",
                column: "AwardId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonAward_FranchiseSeasonId",
                table: "FranchiseSeasonAward",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonAwardWinner_FranchiseSeasonAwardId",
                table: "FranchiseSeasonAwardWinner",
                column: "FranchiseSeasonAwardId");

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
                name: "IX_FranchiseSeasonProjection_FranchiseSeasonId",
                table: "FranchiseSeasonProjection",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRecord_FranchiseSeasonId",
                table: "FranchiseSeasonRecord",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRecordAts_CategoryId",
                table: "FranchiseSeasonRecordAts",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRecordAts_FranchiseSeasonId",
                table: "FranchiseSeasonRecordAts",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRecordStat_FranchiseSeasonRecordId",
                table: "FranchiseSeasonRecordStat",
                column: "FranchiseSeasonRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonStatistic_FranchiseSeasonStatisticCategoryId",
                table: "FranchiseSeasonStatistic",
                column: "FranchiseSeasonStatisticCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonStatisticCategory_FranchiseSeasonId",
                table: "FranchiseSeasonStatisticCategory",
                column: "FranchiseSeasonId");

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
                name: "IX_Play_CompetitionId",
                table: "Play",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Play_DriveId",
                table: "Play",
                column: "DriveId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayExternalId_PlayId",
                table: "PlayExternalId",
                column: "PlayId");

            migrationBuilder.CreateIndex(
                name: "IX_Season_ActivePhaseId",
                table: "Season",
                column: "ActivePhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonExternalId_SeasonId",
                table: "SeasonExternalId",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonFuture_SeasonId",
                table: "SeasonFuture",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonFutureBook_FranchiseSeasonId",
                table: "SeasonFutureBook",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonFutureBook_SeasonFutureItemId",
                table: "SeasonFutureBook",
                column: "SeasonFutureItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonFutureExternalId_SeasonFutureId",
                table: "SeasonFutureExternalId",
                column: "SeasonFutureId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonFutureItem_SeasonFutureId",
                table: "SeasonFutureItem",
                column: "SeasonFutureId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonPhase_SeasonId",
                table: "SeasonPhase",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonPhaseExternalId_SeasonPhaseId",
                table: "SeasonPhaseExternalId",
                column: "SeasonPhaseId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Season_SeasonPhase_ActivePhaseId",
                table: "Season",
                column: "ActivePhaseId",
                principalTable: "SeasonPhase",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Season_SeasonPhase_ActivePhaseId",
                table: "Season");

            migrationBuilder.DropTable(
                name: "AthleteExternalId");

            migrationBuilder.DropTable(
                name: "AthleteImage");

            migrationBuilder.DropTable(
                name: "AthletePositionExternalId");

            migrationBuilder.DropTable(
                name: "AthleteSeason");

            migrationBuilder.DropTable(
                name: "AwardExternalId");

            migrationBuilder.DropTable(
                name: "Broadcast");

            migrationBuilder.DropTable(
                name: "CoachExternalId");

            migrationBuilder.DropTable(
                name: "CoachSeason");

            migrationBuilder.DropTable(
                name: "CompetitionExternalId");

            migrationBuilder.DropTable(
                name: "CompetitionLeaderStat");

            migrationBuilder.DropTable(
                name: "CompetitionLink");

            migrationBuilder.DropTable(
                name: "CompetitionNote");

            migrationBuilder.DropTable(
                name: "CompetitionPowerIndexExternalId");

            migrationBuilder.DropTable(
                name: "CompetitionPrediction");

            migrationBuilder.DropTable(
                name: "CompetitionPredictionValue");

            migrationBuilder.DropTable(
                name: "CompetitionProbabilityExternalId");

            migrationBuilder.DropTable(
                name: "CompetitionStatusExternalId");

            migrationBuilder.DropTable(
                name: "Competitor");

            migrationBuilder.DropTable(
                name: "ContestExternalId");

            migrationBuilder.DropTable(
                name: "ContestLink");

            migrationBuilder.DropTable(
                name: "ContestOdds");

            migrationBuilder.DropTable(
                name: "DriveExternalId");

            migrationBuilder.DropTable(
                name: "FranchiseExternalId");

            migrationBuilder.DropTable(
                name: "FranchiseLogo");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonAwardWinner");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonExternalId");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonLogo");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonProjection");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRecordAts");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRecordStat");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonStatistic");

            migrationBuilder.DropTable(
                name: "GroupExternalId");

            migrationBuilder.DropTable(
                name: "GroupLogo");

            migrationBuilder.DropTable(
                name: "GroupSeasonLogo");

            migrationBuilder.DropTable(
                name: "lkPlayType");

            migrationBuilder.DropTable(
                name: "OutboxMessage");

            migrationBuilder.DropTable(
                name: "OutboxPings");

            migrationBuilder.DropTable(
                name: "PlayExternalId");

            migrationBuilder.DropTable(
                name: "PredictionMetric");

            migrationBuilder.DropTable(
                name: "SeasonExternalId");

            migrationBuilder.DropTable(
                name: "SeasonFutureBook");

            migrationBuilder.DropTable(
                name: "SeasonFutureExternalId");

            migrationBuilder.DropTable(
                name: "SeasonPhaseExternalId");

            migrationBuilder.DropTable(
                name: "VenueExternalId");

            migrationBuilder.DropTable(
                name: "VenueImage");

            migrationBuilder.DropTable(
                name: "Coach");

            migrationBuilder.DropTable(
                name: "Athlete");

            migrationBuilder.DropTable(
                name: "CompetitionLeader");

            migrationBuilder.DropTable(
                name: "CompetitionPowerIndex");

            migrationBuilder.DropTable(
                name: "CompetitionProbability");

            migrationBuilder.DropTable(
                name: "CompetitionStatus");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonAward");

            migrationBuilder.DropTable(
                name: "lkRecordAtsCategory");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRecord");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonStatisticCategory");

            migrationBuilder.DropTable(
                name: "GroupSeason");

            migrationBuilder.DropTable(
                name: "InboxState");

            migrationBuilder.DropTable(
                name: "OutboxState");

            migrationBuilder.DropTable(
                name: "SeasonFutureItem");

            migrationBuilder.DropTable(
                name: "AthletePosition");

            migrationBuilder.DropTable(
                name: "AthleteStatus");

            migrationBuilder.DropTable(
                name: "Location");

            migrationBuilder.DropTable(
                name: "lkLeaderCategory");

            migrationBuilder.DropTable(
                name: "PowerIndex");

            migrationBuilder.DropTable(
                name: "Play");

            migrationBuilder.DropTable(
                name: "Award");

            migrationBuilder.DropTable(
                name: "SeasonFuture");

            migrationBuilder.DropTable(
                name: "Drive");

            migrationBuilder.DropTable(
                name: "Competition");

            migrationBuilder.DropTable(
                name: "CompetitionSource");

            migrationBuilder.DropTable(
                name: "Contest");

            migrationBuilder.DropTable(
                name: "FranchiseSeason");

            migrationBuilder.DropTable(
                name: "Franchise");

            migrationBuilder.DropTable(
                name: "Group");

            migrationBuilder.DropTable(
                name: "Venue");

            migrationBuilder.DropTable(
                name: "SeasonPhase");

            migrationBuilder.DropTable(
                name: "Season");
        }
    }
}
