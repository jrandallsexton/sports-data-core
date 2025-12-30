using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _29DecV1_AtsCatId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "lkRecordAtsCategory",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            // Initialize the identity sequence to start after existing seed data (IDs 1-8)
            // This prevents primary key conflicts when new rows are inserted
            migrationBuilder.Sql(@"
                -- Set the sequence to MAX(Id) + 1 to avoid conflicts with existing data
                SELECT setval(
                    pg_get_serial_sequence('""lkRecordAtsCategory""', 'Id'),
                    COALESCE((SELECT MAX(""Id"") FROM ""lkRecordAtsCategory""), 0) + 1,
                    false
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "lkRecordAtsCategory",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            // Note: When reverting to non-identity column, the sequence is automatically dropped
            // No explicit sequence cleanup needed
        }
    }
}
