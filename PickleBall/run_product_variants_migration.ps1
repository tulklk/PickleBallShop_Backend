# PowerShell script to run ProductVariants migration
# This script will execute the SQL migration file to add ProductVariants support

param(
    [Parameter(Mandatory=$true)]
    [string]$ConnectionString,
    
    [Parameter(Mandatory=$false)]
    [string]$SqlFile = "add_product_variants_migration.sql"
)

Write-Host "Starting ProductVariants migration..." -ForegroundColor Green

# Check if SQL file exists
if (-Not (Test-Path $SqlFile)) {
    Write-Host "Error: SQL file '$SqlFile' not found!" -ForegroundColor Red
    exit 1
}

# Read SQL file content
$sqlContent = Get-Content -Path $SqlFile -Raw

Write-Host "SQL file loaded successfully." -ForegroundColor Green
Write-Host "Connection String: $($ConnectionString.Substring(0, [Math]::Min(50, $ConnectionString.Length)))..." -ForegroundColor Yellow

# For PostgreSQL, you can use psql command
# Make sure you have psql installed and in your PATH
Write-Host ""
Write-Host "To run this migration manually, use one of these methods:" -ForegroundColor Cyan
Write-Host ""
Write-Host "Method 1: Using psql command line:" -ForegroundColor Yellow
Write-Host "  psql `"$ConnectionString`" -f $SqlFile" -ForegroundColor White
Write-Host ""
Write-Host "Method 2: Using dotnet ef migrations:" -ForegroundColor Yellow
Write-Host "  dotnet ef migrations add AddProductVariants" -ForegroundColor White
Write-Host "  dotnet ef database update" -ForegroundColor White
Write-Host ""
Write-Host "Method 3: Execute SQL directly in your database tool (pgAdmin, DBeaver, etc.)" -ForegroundColor Yellow
Write-Host ""

# If psql is available, try to execute
$psqlPath = (Get-Command psql -ErrorAction SilentlyContinue).Source
if ($psqlPath) {
    Write-Host "psql found at: $psqlPath" -ForegroundColor Green
    $response = Read-Host "Do you want to execute the migration now? (Y/N)"
    if ($response -eq "Y" -or $response -eq "y") {
        Write-Host "Executing migration..." -ForegroundColor Green
        try {
            $result = & psql $ConnectionString -f $SqlFile 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Migration completed successfully!" -ForegroundColor Green
            } else {
                Write-Host "Migration failed. Error output:" -ForegroundColor Red
                Write-Host $result
            }
        } catch {
            Write-Host "Error executing migration: $_" -ForegroundColor Red
        }
    }
} else {
    Write-Host "psql not found. Please run the migration manually using one of the methods above." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Migration script prepared. Ready to execute!" -ForegroundColor Green

