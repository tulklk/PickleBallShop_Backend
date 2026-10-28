using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PickleBall.Infrastructure.Data.Migrations;

/// <summary>
/// Security hardening:
///  - Orders.PayOSOrderCode: persistent mapping PayOS order code -> order (replaces in-memory cache).
///  - Users.PasswordResetTokenHash / PasswordResetExpiresAt: single-use password reset tokens (hashed).
///  - RefreshTokens.Token: existing plaintext refresh tokens are replaced by their SHA-256 hex digest.
/// Statements are idempotent because earlier schema changes were applied outside the EF history.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261028000000_SecurityHardening")]
public partial class SecurityHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"Orders\" ADD COLUMN IF NOT EXISTS \"PayOSOrderCode\" bigint NULL;");
        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Orders_PayOSOrderCode\" ON \"Orders\" (\"PayOSOrderCode\") WHERE \"PayOSOrderCode\" IS NOT NULL;");

        migrationBuilder.Sql("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"PasswordResetTokenHash\" character varying(128) NULL;");
        migrationBuilder.Sql("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"PasswordResetExpiresAt\" timestamp with time zone NULL;");

        // Hash refresh tokens at rest. Only rewrite rows that are not already 64-char hex digests.
        migrationBuilder.Sql(
            "UPDATE \"RefreshTokens\" SET \"Token\" = encode(sha256(convert_to(\"Token\", 'UTF8')), 'hex') " +
            "WHERE \"Token\" !~ '^[0-9a-f]{64}$';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Orders_PayOSOrderCode\";");
        migrationBuilder.Sql("ALTER TABLE \"Orders\" DROP COLUMN IF EXISTS \"PayOSOrderCode\";");
        migrationBuilder.Sql("ALTER TABLE \"Users\" DROP COLUMN IF EXISTS \"PasswordResetTokenHash\";");
        migrationBuilder.Sql("ALTER TABLE \"Users\" DROP COLUMN IF EXISTS \"PasswordResetExpiresAt\";");
        // Refresh token hashing is intentionally not reversible; users simply sign in again.
    }
}
