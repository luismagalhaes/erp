using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Sales.Storage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentRectification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RectificationReason",
                table: "SalesDocument",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RectifiedDocumentNumber",
                table: "SalesDocument",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RectificationReason",
                table: "SalesDocument");

            migrationBuilder.DropColumn(
                name: "RectifiedDocumentNumber",
                table: "SalesDocument");
        }
    }
}
