# QR public profile links

To make QR codes scannable from a phone, the link encoded must be reachable from that phone. By default, development uses localhost which only works on the same machine.

Configuration:
- Set `PublicBaseUrl` in `appsettings.Development.json` to a reachable base URL.
  - Example for LAN: `http://<your_pc_ip>:5165`
  - Example for production: `https://your-domain.com`
- The app binds to `http://0.0.0.0:5165` so other devices in the LAN can connect.

Steps to test on a phone (same Wi‑Fi):
1. Find your PC IP address (e.g., 192.168.1.10).
2. Update `PublicBaseUrl` to `http://192.168.1.10:5165` and run the web app.
3. Open `http://192.168.1.10:5165` on your phone’s browser to verify it loads.
4. Generate a student QR and scan with the phone. It should open the public profile page.

Notes:
- If it doesn’t open, check firewall rules for port 5165, and ensure both phone and PC are on the same network.
- Some QR scanners (e.g., Zalo) may attempt to open unrecognized schemes inside the app. If that fails, choose “Open in browser`.”
