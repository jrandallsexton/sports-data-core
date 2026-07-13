using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Notification.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationUserPick : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationUserPicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PickId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Body = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Result = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AttemptedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationUserPicks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationUserPicks_UserId_PickId",
                table: "NotificationUserPicks",
                columns: new[] { "UserId", "PickId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationUserPicks");
        }
    }
}
