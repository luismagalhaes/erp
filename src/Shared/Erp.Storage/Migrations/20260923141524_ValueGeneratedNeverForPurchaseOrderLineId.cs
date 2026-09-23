using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Storage.Migrations
{
    /// <inheritdoc />
    public partial class ValueGeneratedNeverForPurchaseOrderLineId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Model-only change: PurchaseOrderLine.Id was already a plain uniqueidentifier column,
            // and EF Core generates Guid keys client-side either way — nothing to alter in the schema.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // See Up().
        }
    }
}
