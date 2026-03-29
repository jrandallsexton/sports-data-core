using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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
                name: "SeasonPoll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonPoll", x => x.Id);
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
                    Country = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
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
                name: "CoachRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Value = table.Column<double>(type: "double precision", precision: 18, scale: 6, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachRecord_Coach_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coach",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    IsForDarkBg = table.Column<bool>(type: "boolean", nullable: true),
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
                name: "SeasonPollExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonPollId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_SeasonPollExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonPollExternalId_SeasonPoll_SeasonPollId",
                        column: x => x.SeasonPollId,
                        principalTable: "SeasonPoll",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    IsForDarkBg = table.Column<bool>(type: "boolean", nullable: true),
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
                name: "CoachRecordExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachRecordId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CoachRecordExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachRecordExternalId_CoachRecord_CoachRecordId",
                        column: x => x.CoachRecordId,
                        principalTable: "CoachRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoachRecordStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ShortDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Abbreviation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Value = table.Column<double>(type: "double precision", precision: 18, scale: 6, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachRecordStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachRecordStat_CoachRecord_CoachRecordId",
                        column: x => x.CoachRecordId,
                        principalTable: "CoachRecord",
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
                    IsForDarkBg = table.Column<bool>(type: "boolean", nullable: true),
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
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ShortName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WeightLb = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    WeightDisplay = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    HeightIn = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    HeightDisplay = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Jersey = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ExperienceAbbreviation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ExperienceDisplayValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ExperienceYears = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StatusId = table.Column<Guid>(type: "uuid", nullable: true),
                    Discriminator = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteSeason", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteSeason_AthletePosition_PositionId",
                        column: x => x.PositionId,
                        principalTable: "AthletePosition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AthleteSeason_AthleteStatus_StatusId",
                        column: x => x.StatusId,
                        principalTable: "AthleteStatus",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AthleteSeason_Athlete_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athlete",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "AthleteSeasonInjury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TypeDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TypeAbbreviation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Headline = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteSeasonInjury", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteSeasonInjury_AthleteSeason_AthleteSeasonId",
                        column: x => x.AthleteSeasonId,
                        principalTable: "AthleteSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthleteSeasonNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Headline = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteSeasonNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteSeasonNotes_AthleteSeason_AthleteSeasonId",
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
                    SplitId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SplitName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SplitAbbreviation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SplitType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                name: "AthleteSeasonStatisticCategory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonStatisticId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Summary = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
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
                name: "AthleteSeasonStatisticStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonStatisticCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Abbreviation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayValue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PerGameDisplayValue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PerGameValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
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
                name: "AthleteCompetition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    JerseyNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DidNotPlay = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteCompetition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteCompetition_AthletePosition_PositionId",
                        column: x => x.PositionId,
                        principalTable: "AthletePosition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AthleteCompetition_AthleteSeason_AthleteSeasonId",
                        column: x => x.AthleteSeasonId,
                        principalTable: "AthleteSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthleteCompetitionStatistic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteCompetitionStatistic", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteCompetitionStatistic_AthleteSeason_AthleteSeasonId",
                        column: x => x.AthleteSeasonId,
                        principalTable: "AthleteSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthleteCompetitionStatisticCategory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteCompetitionStatisticId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteCompetitionStatisticCategory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteCompetitionStatisticCategory_AthleteCompetitionStati~",
                        column: x => x.AthleteCompetitionStatisticId,
                        principalTable: "AthleteCompetitionStatistic",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthleteCompetitionStatisticStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteCompetitionStatisticCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Abbreviation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    DisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteCompetitionStatisticStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteCompetitionStatisticStat_AthleteCompetitionStatistic~",
                        column: x => x.AthleteCompetitionStatisticCategoryId,
                        principalTable: "AthleteCompetitionStatisticCategory",
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
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                });

            migrationBuilder.CreateTable(
                name: "CoachSeasonRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Value = table.Column<double>(type: "double precision", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachSeasonRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachSeasonRecord_CoachSeason_CoachSeasonId",
                        column: x => x.CoachSeasonId,
                        principalTable: "CoachSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoachSeasonRecordExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachSeasonRecordId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CoachSeasonRecordExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachSeasonRecordExternalId_CoachSeasonRecord_CoachSeasonRe~",
                        column: x => x.CoachSeasonRecordId,
                        principalTable: "CoachSeasonRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoachSeasonRecordStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachSeasonRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ShortDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Abbreviation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Value = table.Column<double>(type: "double precision", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachSeasonRecordStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachSeasonRecordStat_CoachSeasonRecord_CoachSeasonRecordId",
                        column: x => x.CoachSeasonRecordId,
                        principalTable: "CoachSeasonRecord",
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
                        name: "FK_Competition_Venue_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionBroadcast",
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
                    table.PrimaryKey("PK_CompetitionBroadcast", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionBroadcast_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionDrive",
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
                    table.PrimaryKey("PK_CompetitionDrive", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionDrive_Competition_CompetitionId",
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
                name: "CompetitionMetric",
                columns: table => new
                {
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Ypp = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    SuccessRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    ExplosiveRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    PointsPerDrive = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ThirdFourthRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    RzTdRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    RzScoreRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    TimePossRatio = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    OppYpp = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    OppSuccessRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    OppExplosiveRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    OppPointsPerDrive = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    OppThirdFourthRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    OppRzTdRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    OppScoreTdRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    NetPunt = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    FgPctShrunk = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    FieldPosDiff = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    TurnoverMarginPerDrive = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    PenaltyYardsPerPlay = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ComputedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InputsHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionMetric", x => new { x.CompetitionId, x.FranchiseSeasonId });
                    table.ForeignKey(
                        name: "FK_CompetitionMetric_Competition_CompetitionId",
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
                name: "CompetitionOdds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderRef = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderPriority = table.Column<int>(type: "integer", nullable: false),
                    Details = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OverUnder = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Spread = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    OverOdds = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    UnderOdds = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    TotalPointsOpen = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    OverPriceOpen = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    UnderPriceOpen = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    TotalPointsCurrent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    OverPriceCurrent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    UnderPriceCurrent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    TotalPointsClose = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    OverPriceClose = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    UnderPriceClose = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    MoneylineWinner = table.Column<bool>(type: "boolean", nullable: true),
                    SpreadWinner = table.Column<bool>(type: "boolean", nullable: true),
                    WinnerFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    AtsWinnerFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    OverUnderResult = table.Column<int>(type: "integer", nullable: false),
                    EnrichedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PropBetsRef = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClosedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrectedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionOdds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionOdds_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    table.ForeignKey(
                        name: "FK_CompetitionPrediction_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "CompetitionDriveExternalId",
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
                    table.PrimaryKey("PK_CompetitionDriveExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionDriveExternalId_CompetitionDrive_DriveId",
                        column: x => x.DriveId,
                        principalTable: "CompetitionDrive",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionPlay",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriveId = table.Column<Guid>(type: "uuid", nullable: true),
                    EspnId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: true),
                    SequenceNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    TypeId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Text = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ShortText = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AlternativeText = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ShortAlternativeText = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AwayScore = table.Column<int>(type: "integer", nullable: false),
                    HomeScore = table.Column<int>(type: "integer", nullable: false),
                    PeriodNumber = table.Column<int>(type: "integer", nullable: false),
                    ClockValue = table.Column<double>(type: "double precision", nullable: false),
                    ClockDisplayValue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ScoringPlay = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<bool>(type: "boolean", nullable: false),
                    ScoreValue = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartDown = table.Column<int>(type: "integer", nullable: true),
                    StartDistance = table.Column<int>(type: "integer", nullable: true),
                    StartYardLine = table.Column<int>(type: "integer", nullable: true),
                    StartYardsToEndzone = table.Column<int>(type: "integer", nullable: true),
                    StartFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    EndFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_CompetitionPlay", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionPlay_CompetitionDrive_DriveId",
                        column: x => x.DriveId,
                        principalTable: "CompetitionDrive",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionPlay_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionOddsExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionOddsId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CompetitionOddsExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionOddsExternalId_CompetitionOdds_CompetitionOddsId",
                        column: x => x.CompetitionOddsId,
                        principalTable: "CompetitionOdds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionOddsLink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionOddsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Href = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Text = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ShortText = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsExternal = table.Column<bool>(type: "boolean", nullable: false),
                    IsPremium = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionOddsLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionOddsLink_CompetitionOdds_CompetitionOddsId",
                        column: x => x.CompetitionOddsId,
                        principalTable: "CompetitionOdds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionTeamOdds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionOddsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Side = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: true),
                    IsUnderdog = table.Column<bool>(type: "boolean", nullable: true),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    MoneylineOpen = table.Column<int>(type: "integer", nullable: true),
                    MoneylineCurrent = table.Column<int>(type: "integer", nullable: true),
                    MoneylineClose = table.Column<int>(type: "integer", nullable: true),
                    SpreadPointsOpen = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    SpreadPointsCurrent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    SpreadPointsClose = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    SpreadPriceOpen = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    SpreadPriceCurrent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    SpreadPriceClose = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ClosedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrectedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionTeamOdds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionTeamOdds_CompetitionOdds_CompetitionOddsId",
                        column: x => x.CompetitionOddsId,
                        principalTable: "CompetitionOdds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    table.ForeignKey(
                        name: "FK_CompetitionPredictionValue_CompetitionPrediction_Competitio~",
                        column: x => x.CompetitionPredictionId,
                        principalTable: "CompetitionPrediction",
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
                name: "CompetitionPlayExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionPlayId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CompetitionPlayExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionPlayExternalId_CompetitionPlay_CompetitionPlayId",
                        column: x => x.CompetitionPlayId,
                        principalTable: "CompetitionPlay",
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
                        name: "FK_CompetitionProbability_CompetitionPlay_PlayId",
                        column: x => x.PlayId,
                        principalTable: "CompetitionPlay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionProbability_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionSituation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastPlayId = table.Column<Guid>(type: "uuid", nullable: true),
                    Down = table.Column<int>(type: "integer", nullable: false),
                    Distance = table.Column<int>(type: "integer", nullable: false),
                    YardLine = table.Column<int>(type: "integer", nullable: false),
                    IsRedZone = table.Column<bool>(type: "boolean", nullable: false),
                    AwayTimeouts = table.Column<int>(type: "integer", nullable: false),
                    HomeTimeouts = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionSituation", x => x.Id);
                    table.CheckConstraint("CK_CompetitionSituation_AwayTimeouts", "\"AwayTimeouts\" >= 0");
                    table.CheckConstraint("CK_CompetitionSituation_Distance", "\"Distance\" >= -110");
                    table.CheckConstraint("CK_CompetitionSituation_Down", "\"Down\" BETWEEN -1 AND 4");
                    table.CheckConstraint("CK_CompetitionSituation_HomeTimeouts", "\"HomeTimeouts\" >= 0");
                    table.CheckConstraint("CK_CompetitionSituation_YardLine", "\"YardLine\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_CompetitionSituation_CompetitionPlay_LastPlayId",
                        column: x => x.LastPlayId,
                        principalTable: "CompetitionPlay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionSituation_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "CompetitionCompetitor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    HomeAway = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: true),
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
                name: "CompetitionCompetitorRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayValue = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Value = table.Column<double>(type: "double precision", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionCompetitorRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorRecord_CompetitionCompetitor_Competiti~",
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
                name: "CompetitionCompetitorRecordStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ShortDisplayName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Value = table.Column<double>(type: "double precision", nullable: true),
                    DisplayValue = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionCompetitorRecordStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorRecordStat_CompetitionCompetitorRecord~",
                        column: x => x.CompetitionCompetitorRecordId,
                        principalTable: "CompetitionCompetitorRecord",
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
                name: "CompetitionCompetitorStatisticCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorStatisticId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionCompetitorStatisticCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionCompetitorStatisticStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorStatisticCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Abbreviation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    DisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionCompetitorStatisticStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorStatisticStats_CompetitionCompetitorSt~",
                        column: x => x.CompetitionCompetitorStatisticCategoryId,
                        principalTable: "CompetitionCompetitorStatisticCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionCompetitorStatistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorId = table.Column<Guid>(type: "uuid", nullable: true),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionCompetitorStatistics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorStatistics_CompetitionCompetitor_Compe~",
                        column: x => x.CompetitionCompetitorId,
                        principalTable: "CompetitionCompetitor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorStatistics_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionLeaderStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionLeaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<double>(type: "double precision", precision: 18, scale: 6, nullable: false),
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
                        name: "FK_CompetitionLeaderStat_AthleteSeason_AthleteSeasonId",
                        column: x => x.AthleteSeasonId,
                        principalTable: "AthleteSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionLeaderStat_CompetitionLeader_CompetitionLeaderId",
                        column: x => x.CompetitionLeaderId,
                        principalTable: "CompetitionLeader",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                        name: "FK_CompetitionPowerIndex_PowerIndex_PowerIndexId",
                        column: x => x.PowerIndexId,
                        principalTable: "PowerIndex",
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
                name: "CompetitionStream",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonWeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BackgroundJobId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StreamStartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StreamEndedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    ScheduledBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionStream", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionStream_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
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
                    Period = table.Column<int>(type: "integer", nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: true),
                    SeasonWeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonPhaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventNote = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: true),
                    HomeScore = table.Column<int>(type: "integer", nullable: true),
                    AwayScore = table.Column<int>(type: "integer", nullable: true),
                    WinnerFranchiseId = table.Column<Guid>(type: "uuid", nullable: true),
                    SpreadWinnerFranchiseId = table.Column<Guid>(type: "uuid", nullable: true),
                    OverUnder = table.Column<int>(type: "integer", nullable: false),
                    FinalizedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contest_Venue_VenueId",
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
                name: "FranchiseSeason",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupSeasonMap = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    ConferenceWins = table.Column<int>(type: "integer", nullable: false),
                    ConferenceLosses = table.Column<int>(type: "integer", nullable: false),
                    ConferenceTies = table.Column<int>(type: "integer", nullable: false),
                    PtsAllowedMin = table.Column<int>(type: "integer", nullable: true),
                    PtsAllowedMax = table.Column<int>(type: "integer", nullable: true),
                    PtsAllowedAvg = table.Column<decimal>(type: "numeric", nullable: true),
                    PtsScoredMin = table.Column<int>(type: "integer", nullable: true),
                    PtsScoredMax = table.Column<int>(type: "integer", nullable: true),
                    PtsScoredAvg = table.Column<decimal>(type: "numeric", nullable: true),
                    MarginWinMin = table.Column<int>(type: "integer", nullable: true),
                    MarginWinMax = table.Column<int>(type: "integer", nullable: true),
                    MarginWinAvg = table.Column<decimal>(type: "numeric", nullable: true),
                    MarginLossMin = table.Column<int>(type: "integer", nullable: true),
                    MarginLossMax = table.Column<int>(type: "integer", nullable: true),
                    MarginLossAvg = table.Column<decimal>(type: "numeric", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
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
                        name: "FK_FranchiseSeason_Venue_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "FranchiseSeasonLeader",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaderCategoryId = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonLeader", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonLeader_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonLeader_lkLeaderCategory_LeaderCategoryId",
                        column: x => x.LeaderCategoryId,
                        principalTable: "lkLeaderCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    IsForDarkBg = table.Column<bool>(type: "boolean", nullable: true),
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
                name: "FranchiseSeasonMetric",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    GamesPlayed = table.Column<int>(type: "integer", nullable: false),
                    Ypp = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    SuccessRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    ExplosiveRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    PointsPerDrive = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ThirdFourthRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    RzTdRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    RzScoreRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    TimePossRatio = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    OppYpp = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    OppSuccessRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    OppExplosiveRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    OppPointsPerDrive = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    OppThirdFourthRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    OppRzTdRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    OppScoreTdRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    NetPunt = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    FgPctShrunk = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    FieldPosDiff = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    TurnoverMarginPerDrive = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    PenaltyYardsPerPlay = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ComputedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonMetric", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonMetric_FranchiseSeason_FranchiseSeasonId",
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
                name: "FranchiseSeasonLeaderStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonLeaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSeasonLeaderStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonLeaderStat_AthleteSeason_AthleteSeasonId",
                        column: x => x.AthleteSeasonId,
                        principalTable: "AthleteSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FranchiseSeasonLeaderStat_FranchiseSeasonLeader_FranchiseSe~",
                        column: x => x.FranchiseSeasonLeaderId,
                        principalTable: "FranchiseSeasonLeader",
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
                name: "FranchiseSeasonRanking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonWeekId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", maxLength: 40, nullable: true),
                    Headline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ShortHeadline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DefaultRanking = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", maxLength: 40, nullable: true),
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

            migrationBuilder.CreateTable(
                name: "GroupSeason",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MidsizeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsConference = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupSeason", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupSeason_GroupSeason_ParentId",
                        column: x => x.ParentId,
                        principalTable: "GroupSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupSeasonExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    IsForDarkBg = table.Column<bool>(type: "boolean", nullable: true),
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
                name: "SeasonWeek",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonPhaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsNonStandardWeek = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonWeek", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonWeek_SeasonPhase_SeasonPhaseId",
                        column: x => x.SeasonPhaseId,
                        principalTable: "SeasonPhase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeasonWeek_Season_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Season",
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

            migrationBuilder.CreateTable(
                name: "SeasonPollWeek",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonPollId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonWeekId = table.Column<Guid>(type: "uuid", nullable: true),
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
                name: "SeasonWeekExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonWeekId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_SeasonWeekExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonWeekExternalId_SeasonWeek_SeasonWeekId",
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
                name: "IX_AthleteCompetition_AthleteSeasonId",
                table: "AthleteCompetition",
                column: "AthleteSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetition_CompetitionCompetitorId",
                table: "AthleteCompetition",
                column: "CompetitionCompetitorId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetition_CompetitionId_CompetitionCompetitorId_At~",
                table: "AthleteCompetition",
                columns: new[] { "CompetitionId", "CompetitionCompetitorId", "AthleteSeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetition_PositionId",
                table: "AthleteCompetition",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetitionStatistic_AthleteSeasonId_CompetitionId",
                table: "AthleteCompetitionStatistic",
                columns: new[] { "AthleteSeasonId", "CompetitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetitionStatistic_CompetitionId",
                table: "AthleteCompetitionStatistic",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetitionStatisticCategory_AthleteCompetitionStati~",
                table: "AthleteCompetitionStatisticCategory",
                column: "AthleteCompetitionStatisticId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetitionStatisticStat_AthleteCompetitionStatistic~",
                table: "AthleteCompetitionStatisticStat",
                column: "AthleteCompetitionStatisticCategoryId");

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
                name: "IX_AthleteSeasonInjury_AthleteSeasonId",
                table: "AthleteSeasonInjury",
                column: "AthleteSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteSeasonNotes_AthleteSeasonId",
                table: "AthleteSeasonNotes",
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
                name: "IX_AwardExternalId_AwardId",
                table: "AwardExternalId",
                column: "AwardId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachExternalId_CoachId",
                table: "CoachExternalId",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachRecord_CoachId",
                table: "CoachRecord",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachRecordExternalId_CoachRecordId",
                table: "CoachRecordExternalId",
                column: "CoachRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachRecordStat_CoachRecordId",
                table: "CoachRecordStat",
                column: "CoachRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachSeason_CoachId",
                table: "CoachSeason",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachSeason_FranchiseSeasonId",
                table: "CoachSeason",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachSeasonRecord_CoachSeasonId",
                table: "CoachSeasonRecord",
                column: "CoachSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachSeasonRecordExternalId_CoachSeasonRecordId",
                table: "CoachSeasonRecordExternalId",
                column: "CoachSeasonRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachSeasonRecordStat_CoachSeasonRecordId",
                table: "CoachSeasonRecordStat",
                column: "CoachSeasonRecordId");

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
                name: "IX_CompetitionBroadcast_CompetitionId",
                table: "CompetitionBroadcast",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitor_CompetitionId",
                table: "CompetitionCompetitor",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitor_CompetitionId_HomeAway",
                table: "CompetitionCompetitor",
                columns: new[] { "CompetitionId", "HomeAway" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitor_CompetitionId_Order",
                table: "CompetitionCompetitor",
                columns: new[] { "CompetitionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitor_FranchiseSeasonId",
                table: "CompetitionCompetitor",
                column: "FranchiseSeasonId");

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
                name: "IX_CompetitionCompetitorRecord_CompetitionCompetitorId",
                table: "CompetitionCompetitorRecord",
                column: "CompetitionCompetitorId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorRecord_CompetitionCompetitorId_Type",
                table: "CompetitionCompetitorRecord",
                columns: new[] { "CompetitionCompetitorId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorRecordStat_CompetitionCompetitorRecord~",
                table: "CompetitionCompetitorRecordStat",
                column: "CompetitionCompetitorRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorRecordStat_Name",
                table: "CompetitionCompetitorRecordStat",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorScoreExternalIds_CompetitionCompetitor~",
                table: "CompetitionCompetitorScoreExternalIds",
                column: "CompetitionCompetitorScoreId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorScores_CompetitionCompetitorId",
                table: "CompetitionCompetitorScores",
                column: "CompetitionCompetitorId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorStatisticCategories_CompetitionCompeti~",
                table: "CompetitionCompetitorStatisticCategories",
                column: "CompetitionCompetitorStatisticId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorStatistics_CompetitionCompetitorId",
                table: "CompetitionCompetitorStatistics",
                column: "CompetitionCompetitorId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorStatistics_CompetitionId",
                table: "CompetitionCompetitorStatistics",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorStatistics_FranchiseSeasonId",
                table: "CompetitionCompetitorStatistics",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorStatistics_FranchiseSeasonId_Competiti~",
                table: "CompetitionCompetitorStatistics",
                columns: new[] { "FranchiseSeasonId", "CompetitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorStatisticStats_CompetitionCompetitorSt~",
                table: "CompetitionCompetitorStatisticStats",
                column: "CompetitionCompetitorStatisticCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionDrive_CompetitionId",
                table: "CompetitionDrive",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionDriveExternalId_DriveId",
                table: "CompetitionDriveExternalId",
                column: "DriveId");

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
                name: "IX_CompetitionLeaderStat_AthleteSeasonId",
                table: "CompetitionLeaderStat",
                column: "AthleteSeasonId");

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
                name: "IX_CompetitionMetric_Season_FranchiseSeasonId",
                table: "CompetitionMetric",
                columns: new[] { "Season", "FranchiseSeasonId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionNote_CompetitionId",
                table: "CompetitionNote",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionOdds_AtsWinnerFranchiseSeasonId",
                table: "CompetitionOdds",
                column: "AtsWinnerFranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionOdds_CompetitionId",
                table: "CompetitionOdds",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionOdds_CompetitionId_ProviderId",
                table: "CompetitionOdds",
                columns: new[] { "CompetitionId", "ProviderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionOdds_WinnerFranchiseSeasonId",
                table: "CompetitionOdds",
                column: "WinnerFranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionOddsExternalId_CompetitionOddsId",
                table: "CompetitionOddsExternalId",
                column: "CompetitionOddsId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionOddsLink_CompetitionOddsId_Rel_Href",
                table: "CompetitionOddsLink",
                columns: new[] { "CompetitionOddsId", "Rel", "Href" });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPlay_CompetitionId",
                table: "CompetitionPlay",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPlay_DriveId",
                table: "CompetitionPlay",
                column: "DriveId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPlayExternalId_CompetitionPlayId",
                table: "CompetitionPlayExternalId",
                column: "CompetitionPlayId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPowerIndex_CompetitionId_FranchiseSeasonId_Power~",
                table: "CompetitionPowerIndex",
                columns: new[] { "CompetitionId", "FranchiseSeasonId", "PowerIndexId" },
                unique: true);

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
                name: "IX_CompetitionSituation_CompetitionId",
                table: "CompetitionSituation",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionSituation_LastPlayId",
                table: "CompetitionSituation",
                column: "LastPlayId");

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
                name: "IX_CompetitionStream_CompetitionId",
                table: "CompetitionStream",
                column: "CompetitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionStream_SeasonWeekId",
                table: "CompetitionStream",
                column: "SeasonWeekId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTeamOdds_CompetitionOddsId_Side",
                table: "CompetitionTeamOdds",
                columns: new[] { "CompetitionOddsId", "Side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contest_AwayTeamFranchiseSeasonId",
                table: "Contest",
                column: "AwayTeamFranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Contest_HomeTeamFranchiseSeasonId",
                table: "Contest",
                column: "HomeTeamFranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Contest_SeasonWeekId",
                table: "Contest",
                column: "SeasonWeekId");

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
                name: "IX_FranchiseSeason_GroupSeasonId",
                table: "FranchiseSeason",
                column: "GroupSeasonId");

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
                name: "IX_FranchiseSeasonLeader_FranchiseSeasonId",
                table: "FranchiseSeasonLeader",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonLeader_LeaderCategoryId",
                table: "FranchiseSeasonLeader",
                column: "LeaderCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonLeaderStat_AthleteSeasonId",
                table: "FranchiseSeasonLeaderStat",
                column: "AthleteSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonLeaderStat_FranchiseSeasonLeaderId",
                table: "FranchiseSeasonLeaderStat",
                column: "FranchiseSeasonLeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonLogo_FranchiseSeasonId",
                table: "FranchiseSeasonLogo",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonLogo_OriginalUrlHash",
                table: "FranchiseSeasonLogo",
                column: "OriginalUrlHash");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonMetric_FranchiseSeasonId",
                table: "FranchiseSeasonMetric",
                column: "FranchiseSeasonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonProjection_FranchiseSeasonId",
                table: "FranchiseSeasonProjection",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRanking_FranchiseId",
                table: "FranchiseSeasonRanking",
                column: "FranchiseId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRanking_FranchiseSeasonId",
                table: "FranchiseSeasonRanking",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRanking_SeasonWeekId",
                table: "FranchiseSeasonRanking",
                column: "SeasonWeekId");

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
                name: "IX_GroupSeason_ParentId",
                table: "GroupSeason",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupSeason_SeasonId",
                table: "GroupSeason",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupSeason_SeasonYear_Slug",
                table: "GroupSeason",
                columns: new[] { "SeasonYear", "Slug" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupSeasonExternalId_GroupSeasonId",
                table: "GroupSeasonExternalId",
                column: "GroupSeasonId");

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
                name: "IX_PredictionMetric_Abbreviation",
                table: "PredictionMetric",
                column: "Abbreviation");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionMetric_Name",
                table: "PredictionMetric",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PredictionMetric_Name_DisplayName_ShortDisplayName_Abbrevia~",
                table: "PredictionMetric",
                columns: new[] { "Name", "DisplayName", "ShortDisplayName", "Abbreviation", "Description" },
                unique: true);

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
                name: "IX_SeasonPollExternalId_SeasonPollId",
                table: "SeasonPollExternalId",
                column: "SeasonPollId");

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
                name: "IX_SeasonWeek_SeasonId",
                table: "SeasonWeek",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonWeek_SeasonPhaseId",
                table: "SeasonWeek",
                column: "SeasonPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonWeekExternalId_SeasonWeekId",
                table: "SeasonWeekExternalId",
                column: "SeasonWeekId");

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
                name: "FK_AthleteCompetition_CompetitionCompetitor_CompetitionCompeti~",
                table: "AthleteCompetition",
                column: "CompetitionCompetitorId",
                principalTable: "CompetitionCompetitor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AthleteCompetition_Competition_CompetitionId",
                table: "AthleteCompetition",
                column: "CompetitionId",
                principalTable: "Competition",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AthleteCompetitionStatistic_Competition_CompetitionId",
                table: "AthleteCompetitionStatistic",
                column: "CompetitionId",
                principalTable: "Competition",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CoachSeason_FranchiseSeason_FranchiseSeasonId",
                table: "CoachSeason",
                column: "FranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Competition_Contest_ContestId",
                table: "Competition",
                column: "ContestId",
                principalTable: "Contest",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionCompetitor_FranchiseSeason_FranchiseSeasonId",
                table: "CompetitionCompetitor",
                column: "FranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionCompetitorStatisticCategories_CompetitionCompeti~",
                table: "CompetitionCompetitorStatisticCategories",
                column: "CompetitionCompetitorStatisticId",
                principalTable: "CompetitionCompetitorStatistics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionCompetitorStatistics_FranchiseSeason_FranchiseSe~",
                table: "CompetitionCompetitorStatistics",
                column: "FranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionLeaderStat_FranchiseSeason_FranchiseSeasonId",
                table: "CompetitionLeaderStat",
                column: "FranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionMedia_FranchiseSeason_AwayFranchiseSeasonId",
                table: "CompetitionMedia",
                column: "AwayFranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionMedia_FranchiseSeason_HomeFranchiseSeasonId",
                table: "CompetitionMedia",
                column: "HomeFranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionPowerIndex_FranchiseSeason_FranchiseSeasonId",
                table: "CompetitionPowerIndex",
                column: "FranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionStream_SeasonWeek_SeasonWeekId",
                table: "CompetitionStream",
                column: "SeasonWeekId",
                principalTable: "SeasonWeek",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Contest_FranchiseSeason_AwayTeamFranchiseSeasonId",
                table: "Contest",
                column: "AwayTeamFranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contest_FranchiseSeason_HomeTeamFranchiseSeasonId",
                table: "Contest",
                column: "HomeTeamFranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contest_SeasonWeek_SeasonWeekId",
                table: "Contest",
                column: "SeasonWeekId",
                principalTable: "SeasonWeek",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FranchiseSeason_GroupSeason_GroupSeasonId",
                table: "FranchiseSeason",
                column: "GroupSeasonId",
                principalTable: "GroupSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FranchiseSeasonRanking_SeasonWeek_SeasonWeekId",
                table: "FranchiseSeasonRanking",
                column: "SeasonWeekId",
                principalTable: "SeasonWeek",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupSeason_Season_SeasonId",
                table: "GroupSeason",
                column: "SeasonId",
                principalTable: "Season",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
                name: "FK_SeasonPhase_Season_SeasonId",
                table: "SeasonPhase");

            migrationBuilder.DropTable(
                name: "AthleteCompetition");

            migrationBuilder.DropTable(
                name: "AthleteCompetitionStatisticStat");

            migrationBuilder.DropTable(
                name: "AthleteExternalId");

            migrationBuilder.DropTable(
                name: "AthleteImage");

            migrationBuilder.DropTable(
                name: "AthletePositionExternalId");

            migrationBuilder.DropTable(
                name: "AthleteSeasonExternalId");

            migrationBuilder.DropTable(
                name: "AthleteSeasonInjury");

            migrationBuilder.DropTable(
                name: "AthleteSeasonNotes");

            migrationBuilder.DropTable(
                name: "AthleteSeasonStatisticStat");

            migrationBuilder.DropTable(
                name: "AwardExternalId");

            migrationBuilder.DropTable(
                name: "CoachExternalId");

            migrationBuilder.DropTable(
                name: "CoachRecordExternalId");

            migrationBuilder.DropTable(
                name: "CoachRecordStat");

            migrationBuilder.DropTable(
                name: "CoachSeasonRecordExternalId");

            migrationBuilder.DropTable(
                name: "CoachSeasonRecordStat");

            migrationBuilder.DropTable(
                name: "CompetitionBroadcast");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorExternalIds");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorLineScoreExternalId");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorRecordStat");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorScoreExternalIds");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorStatisticStats");

            migrationBuilder.DropTable(
                name: "CompetitionDriveExternalId");

            migrationBuilder.DropTable(
                name: "CompetitionExternalId");

            migrationBuilder.DropTable(
                name: "CompetitionLeaderStat");

            migrationBuilder.DropTable(
                name: "CompetitionLink");

            migrationBuilder.DropTable(
                name: "CompetitionMedia");

            migrationBuilder.DropTable(
                name: "CompetitionMetric");

            migrationBuilder.DropTable(
                name: "CompetitionNote");

            migrationBuilder.DropTable(
                name: "CompetitionOddsExternalId");

            migrationBuilder.DropTable(
                name: "CompetitionOddsLink");

            migrationBuilder.DropTable(
                name: "CompetitionPlayExternalId");

            migrationBuilder.DropTable(
                name: "CompetitionPowerIndexExternalId");

            migrationBuilder.DropTable(
                name: "CompetitionPredictionValue");

            migrationBuilder.DropTable(
                name: "CompetitionProbabilityExternalId");

            migrationBuilder.DropTable(
                name: "CompetitionSituation");

            migrationBuilder.DropTable(
                name: "CompetitionStatusExternalId");

            migrationBuilder.DropTable(
                name: "CompetitionStream");

            migrationBuilder.DropTable(
                name: "CompetitionTeamOdds");

            migrationBuilder.DropTable(
                name: "ContestExternalId");

            migrationBuilder.DropTable(
                name: "ContestLink");

            migrationBuilder.DropTable(
                name: "FranchiseExternalId");

            migrationBuilder.DropTable(
                name: "FranchiseLogo");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonAwardWinner");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonExternalId");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonLeaderStat");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonLogo");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonMetric");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonProjection");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRankingDetailRecordStat");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRankingExternalId");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRankingNote");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRankingOccurrence");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRecordAts");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRecordStat");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonStatistic");

            migrationBuilder.DropTable(
                name: "GroupSeasonExternalId");

            migrationBuilder.DropTable(
                name: "GroupSeasonLogo");

            migrationBuilder.DropTable(
                name: "lkPlayType");

            migrationBuilder.DropTable(
                name: "OutboxMessage");

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
                name: "SeasonPollExternalId");

            migrationBuilder.DropTable(
                name: "SeasonPollWeekEntryStat");

            migrationBuilder.DropTable(
                name: "SeasonPollWeekExternalId");

            migrationBuilder.DropTable(
                name: "SeasonWeekExternalId");

            migrationBuilder.DropTable(
                name: "VenueExternalId");

            migrationBuilder.DropTable(
                name: "VenueImage");

            migrationBuilder.DropTable(
                name: "AthleteCompetitionStatisticCategory");

            migrationBuilder.DropTable(
                name: "AthleteSeasonStatisticCategory");

            migrationBuilder.DropTable(
                name: "CoachRecord");

            migrationBuilder.DropTable(
                name: "CoachSeasonRecord");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorLineScore");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorRecord");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorScores");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorStatisticCategories");

            migrationBuilder.DropTable(
                name: "CompetitionLeader");

            migrationBuilder.DropTable(
                name: "CompetitionPowerIndex");

            migrationBuilder.DropTable(
                name: "CompetitionPrediction");

            migrationBuilder.DropTable(
                name: "CompetitionProbability");

            migrationBuilder.DropTable(
                name: "CompetitionStatus");

            migrationBuilder.DropTable(
                name: "CompetitionOdds");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonAward");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonLeader");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRankingDetailRecord");

            migrationBuilder.DropTable(
                name: "lkRecordAtsCategory");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRecord");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonStatisticCategory");

            migrationBuilder.DropTable(
                name: "InboxState");

            migrationBuilder.DropTable(
                name: "OutboxState");

            migrationBuilder.DropTable(
                name: "SeasonFutureItem");

            migrationBuilder.DropTable(
                name: "SeasonPollWeekEntry");

            migrationBuilder.DropTable(
                name: "AthleteCompetitionStatistic");

            migrationBuilder.DropTable(
                name: "AthleteSeasonStatistic");

            migrationBuilder.DropTable(
                name: "CoachSeason");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorStatistics");

            migrationBuilder.DropTable(
                name: "PowerIndex");

            migrationBuilder.DropTable(
                name: "CompetitionPlay");

            migrationBuilder.DropTable(
                name: "Award");

            migrationBuilder.DropTable(
                name: "lkLeaderCategory");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRankingDetail");

            migrationBuilder.DropTable(
                name: "SeasonFuture");

            migrationBuilder.DropTable(
                name: "SeasonPollWeek");

            migrationBuilder.DropTable(
                name: "AthleteSeason");

            migrationBuilder.DropTable(
                name: "Coach");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitor");

            migrationBuilder.DropTable(
                name: "CompetitionDrive");

            migrationBuilder.DropTable(
                name: "FranchiseSeasonRanking");

            migrationBuilder.DropTable(
                name: "SeasonPoll");

            migrationBuilder.DropTable(
                name: "Athlete");

            migrationBuilder.DropTable(
                name: "Competition");

            migrationBuilder.DropTable(
                name: "AthletePosition");

            migrationBuilder.DropTable(
                name: "AthleteStatus");

            migrationBuilder.DropTable(
                name: "Location");

            migrationBuilder.DropTable(
                name: "CompetitionSource");

            migrationBuilder.DropTable(
                name: "Contest");

            migrationBuilder.DropTable(
                name: "FranchiseSeason");

            migrationBuilder.DropTable(
                name: "SeasonWeek");

            migrationBuilder.DropTable(
                name: "Franchise");

            migrationBuilder.DropTable(
                name: "GroupSeason");

            migrationBuilder.DropTable(
                name: "Venue");

            migrationBuilder.DropTable(
                name: "Season");

            migrationBuilder.DropTable(
                name: "SeasonPhase");
        }
    }
}
