using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PickleBall.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSlugToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Slug column (nullable first to allow adding to existing rows)
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // Generate basic slugs for existing products (simple lowercase, replace spaces)
            migrationBuilder.Sql(@"
                UPDATE ""Products""
                SET ""Slug"" = LOWER(REGEXP_REPLACE(
                    REGEXP_REPLACE(""Name"", '[^a-zA-Z0-9\s-]', '', 'g'),
                    '\s+', '-', 'g'
                ))
                WHERE ""Slug"" IS NULL;
            ");

            // Make Slug NOT NULL after generating values
            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            // Create unique index on Slug
            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the unique index
            migrationBuilder.DropIndex(
                name: "IX_Products_Slug",
                table: "Products");

            // Drop the Slug column
            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Products");
        }
    }
}

