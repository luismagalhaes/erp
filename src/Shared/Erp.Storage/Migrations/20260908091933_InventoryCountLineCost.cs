using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InventoryCountLineCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "InventoryCountLine",
                type: "decimal(19,6)",
                precision: 19,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "InventoryCountLine");
        }
    }
}
