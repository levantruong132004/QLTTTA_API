# PowerShell script to run SQL fix via sqlplus
# Requires Oracle Instant Client with sqlplus installed

$env:NLS_LANG = "AMERICAN_AMERICA.AL32UTF8"

$connectionString = "QLTT_ADMIN/123456@100.118.120.99:1521/orclpdb"
$sqlFile = Join-Path $PSScriptRoot "fix_register_course_proc.sql"

Write-Host "Running SQL fix..." -ForegroundColor Cyan
Write-Host "File: $sqlFile" -ForegroundColor Yellow

# Check if sqlplus is available
$sqlplusPath = Get-Command sqlplus -ErrorAction SilentlyContinue

if (-not $sqlplusPath) {
    Write-Host "ERROR: sqlplus not found. Please install Oracle Instant Client." -ForegroundColor Red
    Write-Host "Download from: https://www.oracle.com/database/technologies/instant-client/downloads.html" -ForegroundColor Yellow
    exit 1
}

# Run SQL
echo "EXIT;" | sqlplus -S $connectionString "@$sqlFile"

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nSUCCESS! Stored procedure has been updated." -ForegroundColor Green
    Write-Host "You can now register courses from the mobile app!" -ForegroundColor Cyan
} else {
    Write-Host "`nERROR: Failed to update stored procedure." -ForegroundColor Red
    Write-Host "Please check the error messages above." -ForegroundColor Yellow
}

Read-Host "`nPress Enter to exit"
