using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Storage.Migrations
{
    /// <inheritdoc />
    public partial class EcoFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EcoFeeForLineId",
                table: "StockMovementLine",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEcoFee",
                table: "StockMovementLine",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "EcoFeeForLineId",
                table: "SalesDocumentLine",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEcoFee",
                table: "SalesDocumentLine",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "PurchaseOrderLine",
                type: "decimal(19,6)",
                precision: 19,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "PurchaseOrderLine",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "PurchaseInvoiceLine",
                type: "decimal(19,6)",
                precision: 19,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "PurchaseInvoiceLine",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "EcoFeeTypeId",
                table: "Product",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EcoFeeWeightKg",
                table: "Product",
                type: "decimal(19,6)",
                precision: 19,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "GoodsReceiptLine",
                type: "decimal(19,6)",
                precision: 19,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "GoodsReceiptLine",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "EcoFeeType",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CalculationBasis = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    ManagingEntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FeeProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcoFeeType", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EcoFeeType_Product_FeeProductId",
                        column: x => x.FeeProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovementLine_EcoFeeForLineId",
                table: "StockMovementLine",
                column: "EcoFeeForLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocumentLine_EcoFeeForLineId",
                table: "SalesDocumentLine",
                column: "EcoFeeForLineId");

            migrationBuilder.CreateIndex(
                name: "IX_Product_EcoFeeTypeId",
                table: "Product",
                column: "EcoFeeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EcoFeeType_CompanyId_Code",
                table: "EcoFeeType",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcoFeeType_FeeProductId",
                table: "EcoFeeType",
                column: "FeeProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Product_EcoFeeType_EcoFeeTypeId",
                table: "Product",
                column: "EcoFeeTypeId",
                principalTable: "EcoFeeType",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesDocumentLine_SalesDocumentLine_EcoFeeForLineId",
                table: "SalesDocumentLine",
                column: "EcoFeeForLineId",
                principalTable: "SalesDocumentLine",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovementLine_StockMovementLine_EcoFeeForLineId",
                table: "StockMovementLine",
                column: "EcoFeeForLineId",
                principalTable: "StockMovementLine",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Product_EcoFeeType_EcoFeeTypeId",
                table: "Product");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesDocumentLine_SalesDocumentLine_EcoFeeForLineId",
                table: "SalesDocumentLine");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovementLine_StockMovementLine_EcoFeeForLineId",
                table: "StockMovementLine");

            migrationBuilder.DropTable(
                name: "EcoFeeType");

            migrationBuilder.DropIndex(
                name: "IX_StockMovementLine_EcoFeeForLineId",
                table: "StockMovementLine");

            migrationBuilder.DropIndex(
                name: "IX_SalesDocumentLine_EcoFeeForLineId",
                table: "SalesDocumentLine");

            migrationBuilder.DropIndex(
                name: "IX_Product_EcoFeeTypeId",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "EcoFeeForLineId",
                table: "StockMovementLine");

            migrationBuilder.DropColumn(
                name: "IsEcoFee",
                table: "StockMovementLine");

            migrationBuilder.DropColumn(
                name: "EcoFeeForLineId",
                table: "SalesDocumentLine");

            migrationBuilder.DropColumn(
                name: "IsEcoFee",
                table: "SalesDocumentLine");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropColumn(
                name: "EcoFeeTypeId",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "EcoFeeWeightKg",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "GoodsReceiptLine");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "GoodsReceiptLine");
        }
    }
}
