using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class _27AugV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "MessageThread",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "DisplayName",
                value: "sportDeets");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThread_CreatedBy",
                table: "MessageThread",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThread_UserId",
                table: "MessageThread",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessagePost_CreatedBy",
                table: "MessagePost",
                column: "CreatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_MessagePost_User_CreatedBy",
                table: "MessagePost",
                column: "CreatedBy",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageThread_User_CreatedBy",
                table: "MessageThread",
                column: "CreatedBy",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageThread_User_UserId",
                table: "MessageThread",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessagePost_User_CreatedBy",
                table: "MessagePost");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageThread_User_CreatedBy",
                table: "MessageThread");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageThread_User_UserId",
                table: "MessageThread");

            migrationBuilder.DropIndex(
                name: "IX_MessageThread_CreatedBy",
                table: "MessageThread");

            migrationBuilder.DropIndex(
                name: "IX_MessageThread_UserId",
                table: "MessageThread");

            migrationBuilder.DropIndex(
                name: "IX_MessagePost_CreatedBy",
                table: "MessagePost");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "MessageThread");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "DisplayName",
                value: "Foo Bar");
        }
    }
}
