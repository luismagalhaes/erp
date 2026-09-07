using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Sales.Storage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceLineOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "OriginatingDate",
                table: "SalesDocumentLine",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginatingLineId",
                table: "SalesDocumentLine",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginatingNumber",
                table: "SalesDocumentLine",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocumentLine_OriginatingLineId",
                table: "SalesDocumentLine",
                column: "OriginatingLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesDocumentLine_StockMovementLine_OriginatingLineId",
                table: "SalesDocumentLine",
                column: "OriginatingLineId",
                principalTable: "StockMovementLine",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesDocumentLine_StockMovementLine_OriginatingLineId",
                table: "SalesDocumentLine");

            migrationBuilder.DropIndex(
                name: "IX_SalesDocumentLine_OriginatingLineId",
                table: "SalesDocumentLine");

            migrationBuilder.DropColumn(
                name: "OriginatingDate",
                table: "SalesDocumentLine");

            migrationBuilder.DropColumn(
                name: "OriginatingLineId",
                table: "SalesDocumentLine");

            migrationBuilder.DropColumn(
                name: "OriginatingNumber",
                table: "SalesDocumentLine");
        }
    }
}
