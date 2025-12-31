using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class _31DecV1_MessagePostUniquePathConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessagePost_ThreadId_Path",
                table: "MessagePost");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MessagePost",
                type: "bytea",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessagePost_ThreadId_Path_Unique",
                table: "MessagePost",
                columns: new[] { "ThreadId", "Path" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessagePost_ThreadId_Path_Unique",
                table: "MessagePost");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MessagePost");

            migrationBuilder.CreateIndex(
                name: "IX_MessagePost_ThreadId_Path",
                table: "MessagePost",
                columns: new[] { "ThreadId", "Path" });
        }
    }
}
