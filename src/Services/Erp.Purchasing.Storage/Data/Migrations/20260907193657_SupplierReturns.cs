using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Purchasing.Storage.Data.Migrations
{
    /// <inheritdoc />
    public partial class SupplierReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReturnLineId",
                table: "PurchaseInvoiceLine",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierReturn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ReturnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SupplierTaxId = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SupplierAddress = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SupplierPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SupplierCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupplierCountry = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    VoidedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturn", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReturnLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ReceiptLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ProductDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    LineAmount = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturnLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLine_SupplierReturn_ReturnId",
                        column: x => x.ReturnId,
                        principalTable: "SupplierReturn",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceLine_ReturnLineId",
                table: "PurchaseInvoiceLine",
                column: "ReturnLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturn_CompanyId_Number",
                table: "SupplierReturn",
                columns: new[] { "CompanyId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturn_CompanyId_SupplierId",
                table: "SupplierReturn",
                columns: new[] { "CompanyId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLine_ReceiptLineId",
                table: "SupplierReturnLine",
                column: "ReceiptLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLine_ReturnId_LineNumber",
                table: "SupplierReturnLine",
                columns: new[] { "ReturnId", "LineNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierReturnLine");

            migrationBuilder.DropTable(
                name: "SupplierReturn");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoiceLine_ReturnLineId",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropColumn(
                name: "ReturnLineId",
                table: "PurchaseInvoiceLine");
        }
    }
}
