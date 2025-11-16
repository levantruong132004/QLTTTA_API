param(
    [int]$Port = 5165
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host "Starting Cloudflare quick tunnel to http://localhost:$Port ..." -ForegroundColor Cyan
Write-Host "If you haven't installed cloudflared, download it here:" -ForegroundColor Yellow
Write-Host "https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/" -ForegroundColor Yellow

# Try to find a local cloudflared binary inside ops/cloudflare first
$localExact = Join-Path $scriptDir 'cloudflared.exe'
$localWildcard = Get-ChildItem -Path $scriptDir -Filter 'cloudflared*.exe' -File -ErrorAction SilentlyContinue | Select-Object -First 1
$cloudflaredPath = $null

if (Test-Path $localExact) {
    $cloudflaredPath = $localExact
} elseif ($null -ne $localWildcard) {
    $cloudflaredPath = $localWildcard.FullName
} else {
    # Fallback to PATH
    $cmd = Get-Command cloudflared -ErrorAction SilentlyContinue
    if ($cmd) { $cloudflaredPath = $cmd.Path }
}

if (-not $cloudflaredPath) {
    Write-Host "cloudflared not found (local or PATH)." -ForegroundColor Red
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  1) Place cloudflared.exe inside ops/cloudflare (same folder as this script)." -ForegroundColor Yellow
    Write-Host "  2) Or install MSI so cloudflared is added to PATH, then open a new PowerShell and retry." -ForegroundColor Yellow
    exit 1
}

Write-Host "Using: $cloudflaredPath" -ForegroundColor Green

# Run cloudflared; this will block and print the public URL in the console
try {
    & $cloudflaredPath tunnel --url "http://localhost:$Port"
}
catch {
    Write-Host "Failed to run cloudflared: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
