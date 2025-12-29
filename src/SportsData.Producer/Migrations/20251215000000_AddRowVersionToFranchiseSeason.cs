using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class AddRowVersionToFranchiseSeason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add xmin-based row versioning column for optimistic concurrency control
            // PostgreSQL's xmin is a system column that automatically tracks row versions
            migrationBuilder.Sql(@"
-- Add RowVersion column mapped to PostgreSQL xmin system column
-- This provides automatic optimistic concurrency control
ALTER TABLE ""FranchiseSeason"" 
ADD COLUMN IF NOT EXISTS ""RowVersion"" xid NOT NULL DEFAULT 0;

-- Note: xmin is automatically maintained by PostgreSQL
-- EF Core will map RowVersion property to xmin for concurrency checks
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""FranchiseSeason"" 
DROP COLUMN IF EXISTS ""RowVersion"";
");
        }
    }
}
