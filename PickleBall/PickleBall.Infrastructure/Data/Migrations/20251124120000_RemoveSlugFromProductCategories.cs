using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PickleBall.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSlugFromProductCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the unique index on Slug
            migrationBuilder.DropIndex(
                name: "IX_ProductCategories_Slug",
                table: "ProductCategories");

            // Drop the Slug column
            migrationBuilder.DropColumn(
                name: "Slug",
                table: "ProductCategories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add back the Slug column
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "ProductCategories",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            // Add back the unique index
            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_Slug",
                table: "ProductCategories",
                column: "Slug",
                unique: true);
        }
    }
}

