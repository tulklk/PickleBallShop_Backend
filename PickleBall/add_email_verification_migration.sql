-- Migration Script: Add Email Verification functionality
-- Execute this script in Supabase SQL Editor

-- 1. Add EmailVerified column to Users table
ALTER TABLE "Users"
ADD COLUMN IF NOT EXISTS "EmailVerified" BOOLEAN NOT NULL DEFAULT FALSE;

-- Add comment to the column
COMMENT ON COLUMN "Users"."EmailVerified" IS 'Indicates whether the user has verified their email address';

-- 2. Create EmailVerificationTokens table
CREATE TABLE IF NOT EXISTS "EmailVerificationTokens" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId" UUID NOT NULL,
    "Token" VARCHAR(500) NOT NULL UNIQUE,
    "ExpiresAt" TIMESTAMPTZ NOT NULL,
    "IsUsed" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "FK_EmailVerificationTokens_Users_UserId" 
        FOREIGN KEY ("UserId") 
        REFERENCES "Users"("Id") 
        ON DELETE CASCADE
);

-- Create index on Token for faster lookups
CREATE INDEX IF NOT EXISTS "IX_EmailVerificationTokens_Token" 
ON "EmailVerificationTokens"("Token");

-- Create index on UserId
CREATE INDEX IF NOT EXISTS "IX_EmailVerificationTokens_UserId" 
ON "EmailVerificationTokens"("UserId");

-- Add comments to the table
COMMENT ON TABLE "EmailVerificationTokens" IS 'Stores email verification tokens for user registration';

-- Verify the changes
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 'Users' 
    AND column_name = 'EmailVerified';

SELECT 
    table_name,
    column_name, 
    data_type
FROM information_schema.columns
WHERE table_name = 'EmailVerificationTokens'
ORDER BY ordinal_position;

