# SalmonLauncher lobby behind Nginx

The lobby is a single dependency-free Node process (`server.mjs`). It keeps rooms in memory, so nothing
needs a database and a restart simply empties the listing.

## 1. Run the process

```bash
# anywhere Node 20+ is available
mkdir -p /www/wwwroot/sl.hugomou.com/lobby
cp server.mjs /www/wwwroot/sl.hugomou.com/lobby/
cd /www/wwwroot/sl.hugomou.com/lobby
nohup node server.mjs 8787 > lobby.log 2>&1 &
```

A systemd unit is the sturdier option:

```ini
# /etc/systemd/system/sl-lobby.service
[Unit]
Description=SalmonLauncher lobby
After=network.target

[Service]
WorkingDirectory=/www/wwwroot/sl.hugomou.com/lobby
ExecStart=/usr/bin/node server.mjs 8787
Restart=always
User=www

[Install]
WantedBy=multi-user.target
```

```bash
systemctl daemon-reload && systemctl enable --now sl-lobby
```

## 2. Expose it at `https://<host>/lobby`

The launcher posts to `https://<host>/lobby/rooms`, so the site must forward that prefix to the process.
Add this inside the existing `server { ... }` block for the site:

```nginx
location /lobby/ {
    proxy_pass http://127.0.0.1:8787/;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_read_timeout 30s;
    client_max_body_size 32k;

    # the lobby answers JSON only; a browser hitting it must not be cached
    add_header Cache-Control "no-store" always;
}
```

Reload Nginx (`nginx -t && systemctl reload nginx`) and check:

```bash
curl -s https://<host>/lobby/rooms        # {"rooms":[]}
```

## 3. Point the launcher at it

In the launcher: **联机 → 联机大厅 → 大厅地址**, for example `https://sl.hugomou.com/lobby`, then turn on
「把我的房间发布到大厅」. Leave the field empty to use the built-in default.

## Notes

- Rooms expire after the lease the launcher asks for (ten minutes, refreshed every minute while hosting);
  a crashed host disappears on its own.
- A password protected room never returns its code from `/rooms`; the code is only released by
  `POST /rooms/:id/reveal` when the client proves the password (SHA-256). The password itself never leaves
  the player's machine.
- The process stores no IP addresses, names or player identifiers.
- `GET /rooms` is the only read endpoint, so a static cache in front of it is fine for a few seconds, but
  `no-store` keeps join latency predictable.
