# Runs a Cloudflare Quick Tunnel to expose local web on port 5165.
# Usage: Right-click > Run with PowerShell or execute in a PS window.

$port = 5165

# Check cloudflared
if (-not (Get-Command cloudflared -ErrorAction SilentlyContinue)) {
  Write-Host "cloudflared is not installed. Install from https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/" -ForegroundColor Yellow
  exit 1
}

Write-Host "Starting Cloudflare Quick Tunnel for http://localhost:$port ..." -ForegroundColor Cyan
cloudflared tunnel --url http://localhost:$port