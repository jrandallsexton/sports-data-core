using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Provider.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecurringJob",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsRecurring = table.Column<bool>(type: "boolean", nullable: false),
                    CronExpression = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    SportId = table.Column<int>(type: "integer", nullable: false),
                    Endpoint = table.Column<string>(type: "text", nullable: false),
                    EndpointMask = table.Column<string>(type: "text", nullable: true),
                    IsSeasonSpecific = table.Column<bool>(type: "boolean", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: true),
                    LastAccessed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastPageIndex = table.Column<int>(type: "integer", nullable: true),
                    TotalPageCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringJob", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledJob",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionMode = table.Column<int>(type: "integer", nullable: false),
                    Href = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SourceDataProvider = table.Column<int>(type: "integer", nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: true),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PollingIntervalInSeconds = table.Column<int>(type: "integer", nullable: true),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: true),
                    TimeoutAfterUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastEnqueuedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastPageIndex = table.Column<int>(type: "integer", nullable: true),
                    TotalPageCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJob", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceIndexItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceIndexId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalUrlHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    LastAccessed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceIndexItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceIndexItem_RecurringJob_ResourceIndexId",
                        column: x => x.ResourceIndexId,
                        principalTable: "RecurringJob",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringJob_Enabled_Provider_Sport_DocumentType_Season",
                table: "RecurringJob",
                columns: new[] { "IsEnabled", "Provider", "SportId", "DocumentType", "SeasonYear" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringJob_Endpoint",
                table: "RecurringJob",
                column: "Endpoint");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringJob_LastAccessed",
                table: "RecurringJob",
                column: "LastAccessed");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceIndexItem_Composite",
                table: "ResourceIndexItem",
                columns: new[] { "ResourceIndexId", "OriginalUrlHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceIndexItem_LastAccessed",
                table: "ResourceIndexItem",
                column: "LastAccessed");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceIndexItem_OriginalUrlHash",
                table: "ResourceIndexItem",
                column: "OriginalUrlHash");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJob_ExecutionMode",
                table: "ScheduledJob",
                column: "ExecutionMode");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJob_Href",
                table: "ScheduledJob",
                column: "Href");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJob_IsActive_StartUtc_EndUtc",
                table: "ScheduledJob",
                columns: new[] { "IsActive", "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJob_SourceDataProvider_Sport_DocumentType",
                table: "ScheduledJob",
                columns: new[] { "SourceDataProvider", "Sport", "DocumentType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceIndexItem");

            migrationBuilder.DropTable(
                name: "ScheduledJob");

            migrationBuilder.DropTable(
                name: "RecurringJob");
        }
    }
}
