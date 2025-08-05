using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class LatestV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PickemGroupConference_PickemGroup_GroupId",
                table: "PickemGroupConference");

            migrationBuilder.DropForeignKey(
                name: "FK_PickemGroupConference_PickemGroup_PickemGroupId",
                table: "PickemGroupConference");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PickemGroupConference",
                table: "PickemGroupConference");

            migrationBuilder.DropIndex(
                name: "IX_PickemGroupConference_GroupId",
                table: "PickemGroupConference");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "PickemGroupConference");

            migrationBuilder.RenameColumn(
                name: "GroupId",
                table: "PickemGroupConference",
                newName: "CreatedBy");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "PickemGroupConference",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConferenceId",
                table: "PickemGroupConference",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ConferenceSlug",
                table: "PickemGroupConference",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "PickemGroupConference",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                table: "PickemGroupConference",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "PickemGroupConference",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PickemGroupConference",
                table: "PickemGroupConference",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 5, 19, 19, 18, 958, DateTimeKind.Utc).AddTicks(6923), new DateTime(2025, 8, 5, 19, 19, 18, 958, DateTimeKind.Utc).AddTicks(7038) });

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupConference_PickemGroupId",
                table: "PickemGroupConference",
                column: "PickemGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_PickemGroupConference_PickemGroup",
                table: "PickemGroupConference",
                column: "PickemGroupId",
                principalTable: "PickemGroup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PickemGroupConference_PickemGroup",
                table: "PickemGroupConference");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PickemGroupConference",
                table: "PickemGroupConference");

            migrationBuilder.DropIndex(
                name: "IX_PickemGroupConference_PickemGroupId",
                table: "PickemGroupConference");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "PickemGroupConference");

            migrationBuilder.DropColumn(
                name: "ConferenceId",
                table: "PickemGroupConference");

            migrationBuilder.DropColumn(
                name: "ConferenceSlug",
                table: "PickemGroupConference");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "PickemGroupConference");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PickemGroupConference");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "PickemGroupConference");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "PickemGroupConference",
                newName: "GroupId");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "PickemGroupConference",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PickemGroupConference",
                table: "PickemGroupConference",
                columns: new[] { "PickemGroupId", "GroupId" });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 5, 14, 18, 52, 337, DateTimeKind.Utc).AddTicks(2540), new DateTime(2025, 8, 5, 14, 18, 52, 337, DateTimeKind.Utc).AddTicks(2657) });

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupConference_GroupId",
                table: "PickemGroupConference",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_PickemGroupConference_PickemGroup_GroupId",
                table: "PickemGroupConference",
                column: "GroupId",
                principalTable: "PickemGroup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PickemGroupConference_PickemGroup_PickemGroupId",
                table: "PickemGroupConference",
                column: "PickemGroupId",
                principalTable: "PickemGroup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
