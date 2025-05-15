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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirebaseUid = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    SignInProvider = table.Column<string>(type: "text", nullable: false),
                    LastLoginUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    Timezone = table.Column<string>(type: "text", nullable: true),
                    CanonicalId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "User",
                columns: new[] { "Id", "CanonicalId", "CreatedBy", "CreatedUtc", "DisplayName", "Email", "EmailVerified", "FirebaseUid", "LastLoginUtc", "ModifiedBy", "ModifiedUtc", "SignInProvider", "Timezone" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), null, new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2025, 5, 15, 9, 14, 37, 276, DateTimeKind.Utc).AddTicks(5428), "Foo Bar", "foo@bar.com", true, "ngovRAr5E8cjMVaZNvcqN1nPFPJ2", new DateTime(2025, 5, 15, 9, 14, 37, 276, DateTimeKind.Utc).AddTicks(5557), null, null, "password", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "User");
        }
    }
}
