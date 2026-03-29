using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PickleBall.Infrastructure.Data.Migrations;

/// <inheritdoc />
[Migration("20260329120000_AddProductIsActiveCategoryIndex")]
public partial class AddProductIsActiveCategoryIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Products_IsActive_CategoryId",
            table: "Products",
            columns: new[] { "IsActive", "CategoryId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Products_IsActive_CategoryId",
            table: "Products");
    }
}
