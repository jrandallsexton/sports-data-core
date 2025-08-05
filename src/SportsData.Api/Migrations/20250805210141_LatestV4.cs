using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class LatestV4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PickemGroupUser");

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

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 5, 21, 1, 40, 892, DateTimeKind.Utc).AddTicks(1840), new DateTime(2025, 8, 5, 21, 1, 40, 892, DateTimeKind.Utc).AddTicks(1967) });

            migrationBuilder.CreateIndex(
                name: "IX_User_FirebaseUid",
                table: "User",
                column: "FirebaseUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupMember_PickemGroupId_UserId",
                table: "PickemGroupMember",
                columns: new[] { "PickemGroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupMember_UserId",
                table: "PickemGroupMember",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PickemGroupMember");

            migrationBuilder.DropIndex(
                name: "IX_User_FirebaseUid",
                table: "User");

            migrationBuilder.CreateTable(
                name: "PickemGroupUser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroupUser", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 5, 19, 25, 19, 474, DateTimeKind.Utc).AddTicks(1946), new DateTime(2025, 8, 5, 19, 25, 19, 474, DateTimeKind.Utc).AddTicks(2067) });

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupUser_PickemGroupId_UserId",
                table: "PickemGroupUser",
                columns: new[] { "PickemGroupId", "UserId" },
                unique: true);
        }
    }
}
