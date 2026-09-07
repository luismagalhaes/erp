using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Purchasing.Storage.Data.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseInvoice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    SupplierDocumentNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SupplierDocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SupplierAtcud = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReverseCharge = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SupplierTaxId = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SupplierAddress = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SupplierPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SupplierCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupplierCountry = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NetTotal = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    TaxTotal = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    GrossTotal = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    VoidedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoice", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ReceiptLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ProductDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    LineAmount = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    TaxCountryRegion = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TaxPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    DeductionNature = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceLine_PurchaseInvoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "PurchaseInvoice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceTaxSummary",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxCountryRegion = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TaxPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxableBase = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceTaxSummary", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceTaxSummary_PurchaseInvoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "PurchaseInvoice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoice_CompanyId_SupplierDocumentDate",
                table: "PurchaseInvoice",
                columns: new[] { "CompanyId", "SupplierDocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoice_CompanyId_SupplierId",
                table: "PurchaseInvoice",
                columns: new[] { "CompanyId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoice_SupplierTaxId",
                table: "PurchaseInvoice",
                column: "SupplierTaxId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceLine_InvoiceId_LineNumber",
                table: "PurchaseInvoiceLine",
                columns: new[] { "InvoiceId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceLine_ReceiptLineId",
                table: "PurchaseInvoiceLine",
                column: "ReceiptLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceTaxSummary_InvoiceId",
                table: "PurchaseInvoiceTaxSummary",
                column: "InvoiceId");

            // Recording the same document from the same supplier twice would deduct the VAT twice.
            // The service refuses it with a readable message; this is what makes it impossible.
            // Written by hand because the index spans a column of the invoice and one of the owned
            // supplier snapshot, which the model builder cannot express — both are columns of this
            // table, so the database enforces it all the same.
            //
            // The number alone is deliberately not unique: two suppliers both issue their FT 2026/1.
            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoice_CompanyId_SupplierTaxId_Number",
                table: "PurchaseInvoice",
                columns: new[] { "CompanyId", "SupplierTaxId", "SupplierDocumentNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseInvoiceLine");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceTaxSummary");

            migrationBuilder.DropTable(
                name: "PurchaseInvoice");
        }
    }
}
