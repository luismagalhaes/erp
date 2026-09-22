using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Identity.Storage.Data.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddSignUpAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignUpAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemoteIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignUpAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignUpAttempts_RemoteIp_OccurredAtUtc",
                table: "SignUpAttempts",
                columns: new[] { "RemoteIp", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignUpAttempts");
        }
    }
}
