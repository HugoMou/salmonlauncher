# Website deployment

The static site lives in `web/` and is served from `https://sl.hugomou.com`.

Pushing to `main` updates the **source only** — it does not publish anything.
The live site keeps serving whatever was last copied to the server, which is
why the site once stayed on 1.0.3 while the repository was already on 2.0.4.

## What is generated, what is committed

| Path | Owner |
| --- | --- |
| `web/*.html`, `web/assets/{css,js,fonts,img}` | committed in the repo, deploy by hand |
| `web/assets/data/releases.json` | **generated** by `scripts/sync-web-releases.sh` on the server |
| `web/downloads/<tag>/…` | **generated** on the server, git-ignored |

Never hand-edit `releases.json` as the source of truth: the sync script
rewrites it from the GitHub API and recomputes every SHA-256. Editing it in the
repository is only useful as a preview.

## Deploying a new version

1. Create the GitHub release first (for example `v2.0.4`) with
   `SL-Setup-<version>-win64.exe` attached. The site has nothing to link to
   until that exists.
2. Copy the committed site files to the web root, skipping the generated
   folders:

   ```bash
   rsync -av --delete \
     --exclude 'downloads/' \
     --exclude 'assets/data/releases.json' \
     web/ /www/wwwroot/sl.hugomou.com/
   ```

3. Run the release sync so the download page and version history pick up the
   new tag:

   ```bash
   WEB_ROOT=/www/wwwroot/sl.hugomou.com /root/sl-sync/sync-web-releases.sh
   ```

4. Check `https://sl.hugomou.com/assets/data/releases.json` lists the new tag
   as the first entry.

The sync script is also on cron, so step 3 happens within a couple of hours on
its own; running it by hand just makes the release visible immediately.

## Verifying a deploy

```bash
curl -s https://sl.hugomou.com/assets/data/releases.json \
  | python3 -c 'import json,sys; d=json.load(sys.stdin); r=d["releases"][0]; print(r["tag"], r["assets"][0]["name"], r["assets"][0]["sha256"])'
```

Compare that SHA-256 with the one attached to the GitHub release; they must
match, since both come from the same uploaded file.

Also open `https://sl.hugomou.com/` and confirm the version in the hero and the
download page match the newest release — if they do not, step 2 was skipped.
