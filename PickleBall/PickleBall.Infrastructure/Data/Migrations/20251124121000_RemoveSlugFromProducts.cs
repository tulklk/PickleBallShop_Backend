using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PickleBall.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSlugFromProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the unique index on Slug
            migrationBuilder.DropIndex(
                name: "IX_Products_Slug",
                table: "Products");

            // Drop the Slug column
            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add back the Slug column
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Products",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            // Add back the unique index
            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true);
        }
    }
}

