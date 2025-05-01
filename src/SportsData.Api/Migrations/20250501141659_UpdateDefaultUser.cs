using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDefaultUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "DisplayName", "Email", "EmailVerified", "FirebaseUid", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 5, 1, 14, 16, 58, 829, DateTimeKind.Utc).AddTicks(5203), "Foo Bar", "foo@bar.com", true, "ngovRAr5E8cjMVaZNvcqN1nPFPJ2", new DateTime(2025, 5, 1, 14, 16, 58, 829, DateTimeKind.Utc).AddTicks(5484) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "DisplayName", "Email", "EmailVerified", "FirebaseUid", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 5, 1, 8, 43, 22, 91, DateTimeKind.Utc).AddTicks(3122), "Randall Sexton", "jrandallsexton@gmail.com", false, "a3GLn01j7pepPpVUSugtKWbRtQG3", new DateTime(2025, 5, 1, 8, 43, 22, 91, DateTimeKind.Utc).AddTicks(3237) });
        }
    }
}
