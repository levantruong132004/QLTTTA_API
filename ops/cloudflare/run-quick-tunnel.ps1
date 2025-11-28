param(
    [int]$Port = 5165,
    [string]$AppSettingsPath = "..\..\QLTTTA_WEB\appsettings.Development.json",
    [switch]$UpdateConfig,
    [switch]$AutoRedirectDev
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

<#
    Enhanced quick tunnel script:
    - Captures the first generated trycloudflare URL.
    - Optionally updates PublicBaseUrl in appsettings.Development.json (use -UpdateConfig).
    - Optionally injects ForceTunnelRedirect=true (use -AutoRedirectDev) so Program.cs can redirect in Development.

    Usage examples:
        ./run-quick-tunnel.ps1 -UpdateConfig -AutoRedirectDev
        ./run-quick-tunnel.ps1 -Port 5165 -UpdateConfig
#>

$cloudUrl = $null
$regex = 'https://[a-z0-9-]+\.trycloudflare\.com'

# Stream output line by line so we can parse and update config early.
try {
    & $cloudflaredPath tunnel --url "http://localhost:$Port" 2>&1 | ForEach-Object {
        Write-Host $_
        if (-not $cloudUrl -and $_ -match $regex) {
            $cloudUrl = $Matches[0]
            Write-Host "Detected Cloudflare URL: $cloudUrl" -ForegroundColor Green
            if ($UpdateConfig) {
                $fullPath = Resolve-Path -Path $AppSettingsPath -ErrorAction SilentlyContinue
                if ($null -eq $fullPath) {
                    Write-Host "Cannot resolve appsettings path: $AppSettingsPath" -ForegroundColor Yellow
                }
                else {
                    try {
                        $jsonText = Get-Content $fullPath -Raw -ErrorAction Stop
                        $cfg = $jsonText | ConvertFrom-Json
                        $cfg.PublicBaseUrl = $cloudUrl
                        if ($AutoRedirectDev) {
                            # Add flag if absent
                            if (-not ($cfg.PSObject.Properties.Name -contains 'ForceTunnelRedirect')) {
                                Add-Member -InputObject $cfg -NotePropertyName ForceTunnelRedirect -NotePropertyValue $true
                            } else { $cfg.ForceTunnelRedirect = $true }
                        }
                        $newJson = $cfg | ConvertTo-Json -Depth 10
                        $newJson | Set-Content $fullPath -Encoding UTF8
                        Write-Host "Updated PublicBaseUrl in $fullPath" -ForegroundColor Cyan
                        if ($AutoRedirectDev) { Write-Host "ForceTunnelRedirect set to true." -ForegroundColor Cyan }
                    }
                    catch {
                        Write-Host "Failed updating config: $($_.Exception.Message)" -ForegroundColor Red
                    }
                }
            }
        }
    }
}
catch {
    Write-Host "Failed to run cloudflared: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
