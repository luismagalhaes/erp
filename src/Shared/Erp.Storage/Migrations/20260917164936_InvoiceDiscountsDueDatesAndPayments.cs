using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceDiscountsDueDatesAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "SalesDocumentLine",
                type: "decimal(19,2)",
                precision: 19,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "SalesDocumentLine",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CustomerCity",
                table: "SalesDocument",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPostalCode",
                table: "SalesDocument",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "SalesDocument",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SalesDocumentPayment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mechanism = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesDocumentPayment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesDocumentPayment_SalesDocument_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "SalesDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocumentPayment_DocumentId",
                table: "SalesDocumentPayment",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesDocumentPayment");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "SalesDocumentLine");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "SalesDocumentLine");

            migrationBuilder.DropColumn(
                name: "CustomerCity",
                table: "SalesDocument");

            migrationBuilder.DropColumn(
                name: "CustomerPostalCode",
                table: "SalesDocument");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "SalesDocument");
        }
    }
}
