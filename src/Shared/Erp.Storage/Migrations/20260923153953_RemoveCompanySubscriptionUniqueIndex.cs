using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Storage.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCompanySubscriptionUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanySubscription_CompanyId",
                table: "CompanySubscription");

            migrationBuilder.CreateIndex(
                name: "IX_CompanySubscription_CompanyId",
                table: "CompanySubscription",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanySubscription_CompanyId",
                table: "CompanySubscription");

            migrationBuilder.CreateIndex(
                name: "IX_CompanySubscription_CompanyId",
                table: "CompanySubscription",
                column: "CompanyId",
                unique: true);
        }
    }
}
