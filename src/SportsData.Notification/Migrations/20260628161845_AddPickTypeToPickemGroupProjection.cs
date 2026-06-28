using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Notification.Migrations
{
    /// <inheritdoc />
    public partial class AddPickTypeToPickemGroupProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PickType",
                table: "PickemGroups",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                // Odds-agnostic default (matches the entity initializer) so
                // existing projected leagues never over-notify on a line move
                // until a backfill refreshes them with the real pick type.
                defaultValue: "StraightUp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PickType",
                table: "PickemGroups");
        }
    }
}
