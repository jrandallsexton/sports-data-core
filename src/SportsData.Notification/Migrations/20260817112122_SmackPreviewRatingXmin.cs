using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Notification.Migrations
{
    /// <inheritdoc />
    public partial class SmackPreviewRatingXmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty. xmin is a PostgreSQL SYSTEM column that
            // exists on every table already; this migration only records the
            // model change (mapping it as the concurrency token) in the
            // snapshot. The scaffolded AddColumn would fail at apply time —
            // the documented Npgsql workaround is to strip the operation.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — see Up().
        }
    }
}
