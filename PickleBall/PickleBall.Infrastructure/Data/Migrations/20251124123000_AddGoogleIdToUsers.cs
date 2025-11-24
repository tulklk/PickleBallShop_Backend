using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PickleBall.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleIdToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add GoogleId column
            migrationBuilder.AddColumn<string>(
                name: "GoogleId",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            // Create unique index on GoogleId (filtered to only non-null values)
            // Using raw SQL because EF Core doesn't fully support filtered unique indexes in all scenarios
            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_GoogleId"" 
                  ON ""Users"" (""GoogleId"") 
                  WHERE ""GoogleId"" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the index
            migrationBuilder.DropIndex(
                name: "IX_Users_GoogleId",
                table: "Users");

            // Drop the GoogleId column
            migrationBuilder.DropColumn(
                name: "GoogleId",
                table: "Users");
        }
    }
}

