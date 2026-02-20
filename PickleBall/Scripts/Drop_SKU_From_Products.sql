-- Sync Products table with current Product entity
-- (no SKU, no ProductType/SkillLevel/WeightRange/SurfaceType/Material; has HasVariants)
-- Run this script on your database (e.g. Supabase SQL Editor) then retry creating a product.

-- 1. Drop the unique index on SKU (required before dropping the column)
DROP INDEX IF EXISTS "IX_Products_SKU";

-- 2. Drop columns that the entity no longer has (from RemovePickleballSpecificFieldsFromProducts)
ALTER TABLE "Products"
  DROP COLUMN IF EXISTS "SKU",
  DROP COLUMN IF EXISTS "ProductType",
  DROP COLUMN IF EXISTS "SkillLevel",
  DROP COLUMN IF EXISTS "WeightRange",
  DROP COLUMN IF EXISTS "SurfaceType",
  DROP COLUMN IF EXISTS "Material";

-- 3. Add HasVariants if missing (entity has HasVariants; migrations may not have added it)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND LOWER(table_name) = 'products' AND LOWER(column_name) = 'hasvariants'
  ) THEN
    ALTER TABLE "Products"
    ADD COLUMN "HasVariants" boolean NOT NULL DEFAULT false;
  END IF;
END $$;
