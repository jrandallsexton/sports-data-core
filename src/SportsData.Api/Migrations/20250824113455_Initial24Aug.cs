using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class Initial24Aug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContestResult",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HomeFranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayFranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    WinningFranchiseId = table.Column<Guid>(type: "uuid", nullable: true),
                    HomeScore = table.Column<int>(type: "integer", nullable: false),
                    AwayScore = table.Column<int>(type: "integer", nullable: false),
                    OverUnder = table.Column<double>(type: "double precision", nullable: true),
                    Spread = table.Column<double>(type: "double precision", nullable: true),
                    WasCanceled = table.Column<bool>(type: "boolean", nullable: false),
                    WentToOvertime = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestResult", x => x.Id);
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
                name: "LeagueStandingHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonWeek = table.Column<int>(type: "integer", nullable: false),
                    TotalPoints = table.Column<int>(type: "integer", nullable: false),
                    CorrectPicks = table.Column<int>(type: "integer", nullable: false),
                    TotalPicks = table.Column<int>(type: "integer", nullable: false),
                    WeeksWon = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    CalculatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueStandingHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeagueWeekResult",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonWeek = table.Column<int>(type: "integer", nullable: false),
                    TotalPoints = table.Column<int>(type: "integer", nullable: false),
                    CorrectPicks = table.Column<int>(type: "integer", nullable: false),
                    TotalPicks = table.Column<int>(type: "integer", nullable: false),
                    IsWeeklyWinner = table.Column<bool>(type: "boolean", nullable: false),
                    CalculatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueWeekResult", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchupPreview",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Overview = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Analysis = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Prediction = table.Column<string>(type: "character varying(768)", maxLength: 768, nullable: true),
                    PredictedStraightUpWinner = table.Column<Guid>(type: "uuid", nullable: true),
                    PredictedSpreadWinner = table.Column<Guid>(type: "uuid", nullable: true),
                    OverUnderPrediction = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchupPreview", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageThread",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PostCount = table.Column<int>(type: "integer", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageThread", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxPings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PingedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "PickemGroup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    League = table.Column<int>(type: "integer", nullable: false),
                    RankingFilter = table.Column<int>(type: "integer", nullable: true),
                    PickType = table.Column<int>(type: "integer", nullable: false),
                    TiebreakerType = table.Column<int>(type: "integer", nullable: false),
                    TiebreakerTiePolicy = table.Column<int>(type: "integer", nullable: false),
                    UseConfidencePoints = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    MaxUsers = table.Column<int>(type: "integer", nullable: true),
                    DropLowWeeksCount = table.Column<int>(type: "integer", nullable: true),
                    CommissionerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PickResult",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserPickId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    PointsAwarded = table.Column<int>(type: "integer", nullable: false),
                    RuleVersion = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickResult", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirebaseUid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    SignInProvider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastLoginUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsSynthetic = table.Column<bool>(type: "boolean", nullable: false),
                    IsPanelPersona = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessagePost",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Depth = table.Column<int>(type: "integer", nullable: false),
                    Path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ReplyCount = table.Column<int>(type: "integer", nullable: false),
                    LikeCount = table.Column<int>(type: "integer", nullable: false),
                    DislikeCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessagePost", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessagePost_MessagePost_ParentId",
                        column: x => x.ParentId,
                        principalTable: "MessagePost",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MessagePost_MessageThread_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "MessageThread",
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
                name: "PickemGroupConference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConferenceSlug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroupConference", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PickemGroupConference_PickemGroup",
                        column: x => x.PickemGroupId,
                        principalTable: "PickemGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PickemGroupWeek",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonWeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonWeek = table.Column<int>(type: "integer", nullable: false),
                    AreMatchupsGenerated = table.Column<bool>(type: "boolean", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroupWeek", x => new { x.GroupId, x.SeasonWeekId });
                    table.ForeignKey(
                        name: "FK_PickemGroupWeek_PickemGroup_GroupId",
                        column: x => x.GroupId,
                        principalTable: "PickemGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PickemGroupInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    PickemGroupId1 = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroupInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PickemGroupInvitations_PickemGroup_PickemGroupId",
                        column: x => x.PickemGroupId,
                        principalTable: "PickemGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PickemGroupInvitations_PickemGroup_PickemGroupId1",
                        column: x => x.PickemGroupId1,
                        principalTable: "PickemGroup",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PickemGroupInvitations_User_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PickemGroupMember",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroupMember", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PickemGroupMember_PickemGroup_PickemGroupId",
                        column: x => x.PickemGroupId,
                        principalTable: "PickemGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PickemGroupMember_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPick",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: false),
                    FranchiseId = table.Column<Guid>(type: "uuid", nullable: true),
                    OverUnder = table.Column<int>(type: "integer", nullable: true),
                    ConfidencePoints = table.Column<int>(type: "integer", nullable: true),
                    PickType = table.Column<int>(type: "integer", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    PointsAwarded = table.Column<int>(type: "integer", nullable: true),
                    WasAgainstSpread = table.Column<bool>(type: "boolean", nullable: true),
                    ScoredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TiebreakerType = table.Column<int>(type: "integer", nullable: false),
                    TiebreakerGuessTotal = table.Column<int>(type: "integer", nullable: true),
                    TiebreakerGuessHome = table.Column<int>(type: "integer", nullable: true),
                    TiebreakerGuessAway = table.Column<int>(type: "integer", nullable: true),
                    TiebreakerActualTotal = table.Column<int>(type: "integer", nullable: true),
                    TiebreakerActualHome = table.Column<int>(type: "integer", nullable: true),
                    TiebreakerActualAway = table.Column<int>(type: "integer", nullable: true),
                    ImportedFromPickId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPick", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPick_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageReaction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageReaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageReaction_MessagePost_PostId",
                        column: x => x.PostId,
                        principalTable: "MessagePost",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PickemGroupMatchup",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonWeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonWeek = table.Column<int>(type: "integer", nullable: false),
                    Spread = table.Column<string>(type: "text", nullable: true),
                    AwaySpread = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: true),
                    AwayRank = table.Column<int>(type: "integer", nullable: true),
                    HomeSpread = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: true),
                    HomeRank = table.Column<int>(type: "integer", nullable: true),
                    OverUnder = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: true),
                    OverOdds = table.Column<double>(type: "double precision", nullable: true),
                    UnderOdds = table.Column<double>(type: "double precision", nullable: true),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroupMatchup", x => new { x.GroupId, x.SeasonWeekId, x.ContestId });
                    table.ForeignKey(
                        name: "FK_PickemGroupMatchup_PickemGroupWeek_GroupId_SeasonWeekId",
                        columns: x => new { x.GroupId, x.SeasonWeekId },
                        principalTable: "PickemGroupWeek",
                        principalColumns: new[] { "GroupId", "SeasonWeekId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "User",
                columns: new[] { "Id", "CreatedBy", "CreatedUtc", "DisplayName", "Email", "EmailVerified", "FirebaseUid", "IsPanelPersona", "IsSynthetic", "LastLoginUtc", "ModifiedBy", "ModifiedUtc", "SignInProvider", "Timezone" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Foo Bar", "foo@bar.com", true, "ngovRAr5E8cjMVaZNvcqN1nPFPJ2", false, false, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "password", null });

            migrationBuilder.CreateIndex(
                name: "IX_ContestResult_ContestId",
                table: "ContestResult",
                column: "ContestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContestResult_Sport_SeasonYear",
                table: "ContestResult",
                columns: new[] { "Sport", "SeasonYear" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueStandingHistory_PickemGroupId_UserId_SeasonYear_Seaso~",
                table: "LeagueStandingHistory",
                columns: new[] { "PickemGroupId", "UserId", "SeasonYear", "SeasonWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueWeekResult_PickemGroupId_SeasonYear_SeasonWeek_UserId",
                table: "LeagueWeekResult",
                columns: new[] { "PickemGroupId", "SeasonYear", "SeasonWeek", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchupPreview_ContestId",
                table: "MatchupPreview",
                column: "ContestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessagePost_ParentId",
                table: "MessagePost",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_MessagePost_ThreadId_ParentId",
                table: "MessagePost",
                columns: new[] { "ThreadId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessagePost_ThreadId_Path",
                table: "MessagePost",
                columns: new[] { "ThreadId", "Path" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageReaction_PostId_Type",
                table: "MessageReaction",
                columns: new[] { "PostId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageReaction_PostId_UserId",
                table: "MessageReaction",
                columns: new[] { "PostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageReaction_UserId",
                table: "MessageReaction",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThread_GroupId_LastActivityAt",
                table: "MessageThread",
                columns: new[] { "GroupId", "LastActivityAt" });

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
                name: "IX_PickemGroup_CommissionerUserId",
                table: "PickemGroup",
                column: "CommissionerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupConference_PickemGroupId",
                table: "PickemGroupConference",
                column: "PickemGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupInvitations_InvitedByUserId",
                table: "PickemGroupInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupInvitations_PickemGroupId",
                table: "PickemGroupInvitations",
                column: "PickemGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupInvitations_PickemGroupId1",
                table: "PickemGroupInvitations",
                column: "PickemGroupId1");

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupMatchup_GroupId_ContestId",
                table: "PickemGroupMatchup",
                columns: new[] { "GroupId", "ContestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupMatchup_GroupId_SeasonYear_SeasonWeek",
                table: "PickemGroupMatchup",
                columns: new[] { "GroupId", "SeasonYear", "SeasonWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupMember_PickemGroupId_UserId",
                table: "PickemGroupMember",
                columns: new[] { "PickemGroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupMember_UserId",
                table: "PickemGroupMember",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupWeek_SeasonYear_SeasonWeek",
                table: "PickemGroupWeek",
                columns: new[] { "SeasonYear", "SeasonWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_PickResult_UserPickId",
                table: "PickResult",
                column: "UserPickId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_FirebaseUid",
                table: "User",
                column: "FirebaseUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPick_ContestId",
                table: "UserPick",
                column: "ContestId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPick_PickemGroupId_UserId_ContestId",
                table: "UserPick",
                columns: new[] { "PickemGroupId", "UserId", "ContestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPick_UserId",
                table: "UserPick",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContestResult");

            migrationBuilder.DropTable(
                name: "LeagueStandingHistory");

            migrationBuilder.DropTable(
                name: "LeagueWeekResult");

            migrationBuilder.DropTable(
                name: "MatchupPreview");

            migrationBuilder.DropTable(
                name: "MessageReaction");

            migrationBuilder.DropTable(
                name: "OutboxMessage");

            migrationBuilder.DropTable(
                name: "OutboxPings");

            migrationBuilder.DropTable(
                name: "PickemGroupConference");

            migrationBuilder.DropTable(
                name: "PickemGroupInvitations");

            migrationBuilder.DropTable(
                name: "PickemGroupMatchup");

            migrationBuilder.DropTable(
                name: "PickemGroupMember");

            migrationBuilder.DropTable(
                name: "PickResult");

            migrationBuilder.DropTable(
                name: "UserPick");

            migrationBuilder.DropTable(
                name: "MessagePost");

            migrationBuilder.DropTable(
                name: "InboxState");

            migrationBuilder.DropTable(
                name: "OutboxState");

            migrationBuilder.DropTable(
                name: "PickemGroupWeek");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "MessageThread");

            migrationBuilder.DropTable(
                name: "PickemGroup");
        }
    }
}
