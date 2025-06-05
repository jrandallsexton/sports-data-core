using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Provider.Migrations
{
    /// <inheritdoc />
    public partial class IsQueued : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsQueued",
                table: "ResourceIndex",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsQueued",
                table: "ResourceIndex");
        }
    }
}
