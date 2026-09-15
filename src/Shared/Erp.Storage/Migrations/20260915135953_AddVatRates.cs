using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddVatRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VatRate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalRegion = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatRate", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VatRate_FiscalRegion_Code",
                table: "VatRate",
                columns: new[] { "FiscalRegion", "Code" },
                unique: true);

            // The 12 rates set by Portuguese law for the three fiscal regions — seeded once here,
            // not re-applied on every startup like Identity's clients/roles, so an edit through the
            // backoffice page sticks.
            migrationBuilder.InsertData(
                table: "VatRate",
                columns: new[] { "Id", "FiscalRegion", "Code", "Label", "Percentage", "IsActive" },
                values: new object[,]
                {
                    { new Guid("9f5a1a10-0001-4a00-8000-000000000001"), "PT", "NOR", "Normal", 23.00m, true },
                    { new Guid("9f5a1a10-0001-4a00-8000-000000000002"), "PT", "INT", "Intermédia", 13.00m, true },
                    { new Guid("9f5a1a10-0001-4a00-8000-000000000003"), "PT", "RED", "Reduzida", 6.00m, true },
                    { new Guid("9f5a1a10-0001-4a00-8000-000000000004"), "PT", "ISE", "Isento", 0.00m, true },
                    { new Guid("9f5a1a10-0002-4a00-8000-000000000001"), "PT-AC", "NOR", "Normal", 16.00m, true },
                    { new Guid("9f5a1a10-0002-4a00-8000-000000000002"), "PT-AC", "INT", "Intermédia", 9.00m, true },
                    { new Guid("9f5a1a10-0002-4a00-8000-000000000003"), "PT-AC", "RED", "Reduzida", 4.00m, true },
                    { new Guid("9f5a1a10-0002-4a00-8000-000000000004"), "PT-AC", "ISE", "Isento", 0.00m, true },
                    { new Guid("9f5a1a10-0003-4a00-8000-000000000001"), "PT-MA", "NOR", "Normal", 22.00m, true },
                    { new Guid("9f5a1a10-0003-4a00-8000-000000000002"), "PT-MA", "INT", "Intermédia", 12.00m, true },
                    { new Guid("9f5a1a10-0003-4a00-8000-000000000003"), "PT-MA", "RED", "Reduzida", 5.00m, true },
                    { new Guid("9f5a1a10-0003-4a00-8000-000000000004"), "PT-MA", "ISE", "Isento", 0.00m, true }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VatRate");
        }
    }
}
