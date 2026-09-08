using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Storage.Migrations
{
    /// <inheritdoc />
    public partial class SelfBilledInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SelfBilling",
                table: "Series",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SelfBilledInvoice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Atcud = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SystemEntryDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    AcceptedBySupplierAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupplierAgreementReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupplierCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SupplierTaxId = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SupplierAddress = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SupplierPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SupplierCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupplierCountry = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NetTotal = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    TaxPayable = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    GrossTotal = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreviousHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HashControl = table.Column<string>(type: "nvarchar(70)", maxLength: 70, nullable: false),
                    QrCodePayload = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfBilledInvoice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SelfBilledInvoice_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SelfBilledInvoiceLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ReceiptLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    TaxExemptionCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TaxExemptionReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfBilledInvoiceLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SelfBilledInvoiceLine_SelfBilledInvoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "SelfBilledInvoice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SelfBilledInvoiceStatusChange",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfBilledInvoiceStatusChange", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SelfBilledInvoiceStatusChange_SelfBilledInvoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "SelfBilledInvoice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SelfBilledInvoiceTaxSummary",
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
                    table.PrimaryKey("PK_SelfBilledInvoiceTaxSummary", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SelfBilledInvoiceTaxSummary_SelfBilledInvoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "SelfBilledInvoice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SelfBilledInvoice_CompanyId_DocumentNumber",
                table: "SelfBilledInvoice",
                columns: new[] { "CompanyId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SelfBilledInvoice_CompanyId_IssueDate",
                table: "SelfBilledInvoice",
                columns: new[] { "CompanyId", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SelfBilledInvoice_CompanyId_SupplierId",
                table: "SelfBilledInvoice",
                columns: new[] { "CompanyId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_SelfBilledInvoice_SeriesId_SequenceNumber",
                table: "SelfBilledInvoice",
                columns: new[] { "SeriesId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SelfBilledInvoice_SupplierTaxId",
                table: "SelfBilledInvoice",
                column: "SupplierTaxId");

            migrationBuilder.CreateIndex(
                name: "IX_SelfBilledInvoiceLine_InvoiceId_LineNumber",
                table: "SelfBilledInvoiceLine",
                columns: new[] { "InvoiceId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SelfBilledInvoiceLine_ReceiptLineId",
                table: "SelfBilledInvoiceLine",
                column: "ReceiptLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SelfBilledInvoiceStatusChange_InvoiceId",
                table: "SelfBilledInvoiceStatusChange",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SelfBilledInvoiceTaxSummary_InvoiceId",
                table: "SelfBilledInvoiceTaxSummary",
                column: "InvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SelfBilledInvoiceLine");

            migrationBuilder.DropTable(
                name: "SelfBilledInvoiceStatusChange");

            migrationBuilder.DropTable(
                name: "SelfBilledInvoiceTaxSummary");

            migrationBuilder.DropTable(
                name: "SelfBilledInvoice");

            migrationBuilder.DropColumn(
                name: "SelfBilling",
                table: "Series");
        }
    }
}
