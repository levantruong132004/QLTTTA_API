# Cloudflare Tunnel setup for QLTTTA_WEB

This guide gives you a stable public HTTPS URL for the local web app (port 5165) so QR codes work from any network.

Prereqs:
- A Cloudflare account (free) and a domain managed by Cloudflare DNS (recommended). Alternatively use Quick Tunnel (no domain).
- Windows PowerShell on the dev machine.

## Option A: Quick Tunnel (no domain, temporary URL)
1. Install cloudflared:
   - Download from https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/
   - Or via Scoop: `scoop install cloudflared`
2. Run a quick tunnel to local web:
   ```powershell
   cloudflared tunnel --url http://localhost:5165
   ```
3. Note the `https://<random>.trycloudflare.com` URL printed.
4. Set `PublicBaseUrl` in `QLTTTA_WEB/appsettings.Development.json` to that HTTPS URL and regenerate QR.

## Option B: Permanent Tunnel (requires your domain)
1. Login once:
   ```powershell
   cloudflared tunnel login
   ```
2. Create a named tunnel:
   ```powershell
   cloudflared tunnel create qlttta-web
   ```
3. Create DNS route for a hostname (replace with yours):
   ```powershell
   cloudflared tunnel route dns qlttta-web profile.yourdomain.com
   ```
4. Create config file `%USERPROFILE%/.cloudflared/config.yml` (use the generated tunnel ID json path):
   ```yaml
   tunnel: qlttta-web
   credentials-file: C:\\Users\\<YOU>\\.cloudflared\\<TUNNEL_ID>.json
   ingress:
     - hostname: profile.yourdomain.com
       service: http://localhost:5165
     - service: http_status:404
   ```
5. Start the tunnel:
   ```powershell
   cloudflared tunnel run qlttta-web
   ```
6. Set `PublicBaseUrl` in `QLTTTA_WEB/appsettings.Development.json` to `https://profile.yourdomain.com` and regenerate QR.

## Helper scripts
Use `run-quick-tunnel.ps1` for a one-liner quick tunnel.

## Notes
- Always use HTTPS URL for better compatibility with in-app browsers.
- The API still runs on localhost:7158; the web app calls it server-side, so no extra CORS.
- If you stop/restart Quick Tunnel, the URL will change—update `PublicBaseUrl` and regenerate QR.
