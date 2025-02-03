using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Provider.Migrations
{
    /// <inheritdoc />
    public partial class ResourceIndexItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResourceIndexItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceIndexId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalUrlHash = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastAccessed = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceIndexItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceIndexItem_ResourceIndex_ResourceIndexId",
                        column: x => x.ResourceIndexId,
                        principalTable: "ResourceIndex",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceIndexItem_OriginalUrlHash",
                table: "ResourceIndexItem",
                column: "OriginalUrlHash");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceIndexItem_ResourceIndexId",
                table: "ResourceIndexItem",
                column: "ResourceIndexId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceIndexItem");
        }
    }
}
