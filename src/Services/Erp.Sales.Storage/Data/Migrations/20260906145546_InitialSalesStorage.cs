using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Sales.Storage.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSalesStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sales");

            migrationBuilder.CreateTable(
                name: "Series",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstablishmentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    SeriesCode = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    InitialSequence = table.Column<int>(type: "int", nullable: false),
                    CurrentSequence = table.Column<int>(type: "int", nullable: false),
                    ValidationCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    CommunicatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinalizedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesDocument",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Atcud = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SystemEntryDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    SourceBilling = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    CustomerTaxId = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerAddress = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CustomerCountry = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NetTotal = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    TaxPayable = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    GrossTotal = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreviousHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HashControl = table.Column<string>(type: "nvarchar(70)", maxLength: 70, nullable: false),
                    QrCodePayload = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    RectifiedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesDocument", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesDocument_SalesDocument_RectifiedDocumentId",
                        column: x => x.RectifiedDocumentId,
                        principalSchema: "sales",
                        principalTable: "SalesDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesDocument_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalSchema: "sales",
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentStatusChange",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    NewStatus = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentStatusChange", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentStatusChange_SalesDocument_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "sales",
                        principalTable: "SalesDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTaxSummary",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxCountryRegion = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TaxPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxableBase = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTaxSummary", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTaxSummary_SalesDocument_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "sales",
                        principalTable: "SalesDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesDocumentLine",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ProductDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    LineAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    TaxCountryRegion = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TaxPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    TaxExemptionCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TaxExemptionReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesDocumentLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesDocumentLine_SalesDocument_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "sales",
                        principalTable: "SalesDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentStatusChange_DocumentId",
                schema: "sales",
                table: "DocumentStatusChange",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTaxSummary_DocumentId",
                schema: "sales",
                table: "DocumentTaxSummary",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocument_CompanyId_DocumentDate",
                schema: "sales",
                table: "SalesDocument",
                columns: new[] { "CompanyId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocument_CompanyId_DocumentNumber",
                schema: "sales",
                table: "SalesDocument",
                columns: new[] { "CompanyId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocument_RectifiedDocumentId",
                schema: "sales",
                table: "SalesDocument",
                column: "RectifiedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocument_SeriesId_SequenceNumber",
                schema: "sales",
                table: "SalesDocument",
                columns: new[] { "SeriesId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocumentLine_DocumentId_LineNumber",
                schema: "sales",
                table: "SalesDocumentLine",
                columns: new[] { "DocumentId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Series_CompanyId",
                schema: "sales",
                table: "Series",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Series_CompanyId_DocumentType_SeriesCode",
                schema: "sales",
                table: "Series",
                columns: new[] { "CompanyId", "DocumentType", "SeriesCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentStatusChange",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "DocumentTaxSummary",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SalesDocumentLine",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SalesDocument",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "Series",
                schema: "sales");
        }
    }
}
