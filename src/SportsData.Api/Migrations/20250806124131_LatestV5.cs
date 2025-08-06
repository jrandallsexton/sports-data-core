using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class LatestV5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxUsers",
                table: "PickemGroup",
                type: "integer",
                nullable: true);

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

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 6, 12, 41, 31, 403, DateTimeKind.Utc).AddTicks(1620), new DateTime(2025, 8, 6, 12, 41, 31, 403, DateTimeKind.Utc).AddTicks(1740) });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PickemGroupInvitations");

            migrationBuilder.DropColumn(
                name: "MaxUsers",
                table: "PickemGroup");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 5, 21, 1, 40, 892, DateTimeKind.Utc).AddTicks(1840), new DateTime(2025, 8, 5, 21, 1, 40, 892, DateTimeKind.Utc).AddTicks(1967) });
        }
    }
}
