using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Storage.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEcoFeeTypeFeeProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EcoFeeType_Product_FeeProductId",
                table: "EcoFeeType");

            migrationBuilder.DropIndex(
                name: "IX_EcoFeeType_FeeProductId",
                table: "EcoFeeType");

            migrationBuilder.DropColumn(
                name: "FeeProductId",
                table: "EcoFeeType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FeeProductId",
                table: "EcoFeeType",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.CreateIndex(
                name: "IX_EcoFeeType_FeeProductId",
                table: "EcoFeeType",
                column: "FeeProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_EcoFeeType_Product_FeeProductId",
                table: "EcoFeeType",
                column: "FeeProductId",
                principalTable: "Product",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
