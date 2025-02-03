using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Provider.Migrations
{
    /// <inheritdoc />
    public partial class ResourceIndexPagination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastPageIndex",
                table: "ResourceIndex",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalPageCount",
                table: "ResourceIndex",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPageIndex",
                table: "ResourceIndex");

            migrationBuilder.DropColumn(
                name: "TotalPageCount",
                table: "ResourceIndex");
        }
    }
}
