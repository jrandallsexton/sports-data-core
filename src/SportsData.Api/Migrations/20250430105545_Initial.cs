using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirebaseUid = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EmailVerified = table.Column<bool>(type: "bit", nullable: false),
                    SignInProvider = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastLoginUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CanonicalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "User",
                columns: new[] { "Id", "CanonicalId", "CreatedBy", "CreatedUtc", "DisplayName", "Email", "EmailVerified", "FirebaseUid", "LastLoginUtc", "ModifiedBy", "ModifiedUtc", "SignInProvider" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), null, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2025, 4, 30, 10, 55, 44, 710, DateTimeKind.Utc).AddTicks(1093), "Randall Sexton", "jrandallsexton@gmail.com", false, "a3GLn01j7pepPpVUSugtKWbRtQG3", new DateTime(2025, 4, 30, 10, 55, 44, 710, DateTimeKind.Utc).AddTicks(1182), null, null, "password" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "User");
        }
    }
}
