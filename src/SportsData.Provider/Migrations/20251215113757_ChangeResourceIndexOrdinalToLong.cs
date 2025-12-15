using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Provider.Migrations
{
    /// <inheritdoc />
    public partial class ChangeResourceIndexOrdinalToLong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "Ordinal",
                table: "ResourceIndex",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Ordinal",
                table: "ResourceIndex",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
