using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Venue.Migrations
{
    /// <inheritdoc />
    public partial class EntityChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GlobalId",
                table: "Venue",
                newName: "CanonicalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CanonicalId",
                table: "Venue",
                newName: "GlobalId");
        }
    }
}
