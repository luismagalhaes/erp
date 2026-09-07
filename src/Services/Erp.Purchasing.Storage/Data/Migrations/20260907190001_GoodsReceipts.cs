using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Purchasing.Storage.Data.Migrations
{
    /// <inheritdoc />
    public partial class GoodsReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoodsReceipt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    SupplierDocumentNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SupplierDocumentDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_GoodsReceipt", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ProductDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    LineAmount = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptLine_GoodsReceipt_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "GoodsReceipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipt_CompanyId_Number",
                table: "GoodsReceipt",
                columns: new[] { "CompanyId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipt_CompanyId_ReceiptDate",
                table: "GoodsReceipt",
                columns: new[] { "CompanyId", "ReceiptDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipt_CompanyId_SupplierId",
                table: "GoodsReceipt",
                columns: new[] { "CompanyId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLine_OrderLineId",
                table: "GoodsReceiptLine",
                column: "OrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLine_ReceiptId_LineNumber",
                table: "GoodsReceiptLine",
                columns: new[] { "ReceiptId", "LineNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoodsReceiptLine");

            migrationBuilder.DropTable(
                name: "GoodsReceipt");
        }
    }
}
