using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Core.Storage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductInventoryCategory : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Existing products become merchandise. An empty string would be neither a category the
        /// schema knows nor an honest answer, and every row already on file has to have one.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InventoryCategory",
                table: "Products",
                type: "nchar(1)",
                fixedLength: true,
                maxLength: 1,
                nullable: false,
                defaultValue: "M");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InventoryCategory",
                table: "Products");
        }
    }
}
