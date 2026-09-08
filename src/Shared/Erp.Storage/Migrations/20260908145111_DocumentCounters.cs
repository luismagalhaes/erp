using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Storage.Migrations
{
    /// <inheritdoc />
    public partial class DocumentCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentCounter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentCounter", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCounter_CompanyId_Prefix_Year",
                table: "DocumentCounter",
                columns: new[] { "CompanyId", "Prefix", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentCounter");
        }
    }
}
