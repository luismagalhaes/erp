using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Sales.Storage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockMovement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovementType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Atcud = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MovementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SystemEntryDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    SourceBilling = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    PartyTaxId = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PartyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartyIsSupplier = table.Column<bool>(type: "bit", nullable: false),
                    MovementStartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MovementEndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VehiclePlate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    TotalQuantity = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    NetTotal = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    TaxPayable = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    GrossTotal = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreviousHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HashControl = table.Column<string>(type: "nvarchar(70)", maxLength: 70, nullable: false),
                    QrCodePayload = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    AtDocCodeId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CommunicatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ShipFromAddress = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ShipFromCity = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ShipFromCountry = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ShipFromLocationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ShipFromPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ShipFromWarehouseId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ShipToAddress = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ShipToCity = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ShipToCountry = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ShipToLocationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ShipToPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ShipToWarehouseId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovement_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovementStatusChange",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    NewStatus = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementStatusChange", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovementStatusChange_StockMovement_MovementId",
                        column: x => x.MovementId,
                        principalTable: "StockMovement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovementLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_StockMovementLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovementLine_StockMovement_MovementId",
                        column: x => x.MovementId,
                        principalTable: "StockMovement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovementStatusChange_MovementId",
                table: "MovementStatusChange",
                column: "MovementId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovement_CompanyId_DocumentNumber",
                table: "StockMovement",
                columns: new[] { "CompanyId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovement_CompanyId_MovementDate",
                table: "StockMovement",
                columns: new[] { "CompanyId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovement_SeriesId_SequenceNumber",
                table: "StockMovement",
                columns: new[] { "SeriesId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovementLine_MovementId_LineNumber",
                table: "StockMovementLine",
                columns: new[] { "MovementId", "LineNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovementStatusChange");

            migrationBuilder.DropTable(
                name: "StockMovementLine");

            migrationBuilder.DropTable(
                name: "StockMovement");
        }
    }
}
