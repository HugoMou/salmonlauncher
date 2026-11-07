// SalmonLauncher (SL) — a Minecraft Java edition launcher
// Copyright (C) 2026 Hugo Mou
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, either version 3 of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY
// WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A
// PARTICULAR PURPOSE. See the GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License along with
// this program. If not, see <https://www.gnu.org/licenses/>.
//
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Minimal lobby service for SalmonLauncher.
//
//   node server.mjs [port]        (default port 8787, PORT env also works)
//
// Rooms live in memory only: a room is dropped as soon as its time-to-live passes,
// which happens automatically when the host stops refreshing it. Nothing is logged
// about players, and the password is never sent to this process — only its SHA-256.
//
// API
//   POST   /rooms              {code,name,notice,tags,players,maxPlayers,version,passwordHash,secret,ttl}
//                              -> {ok:true,id}
//   GET    /rooms              -> {rooms:[{id,name,notice,tags,players,maxPlayers,version,hasPassword,code}]}
//   POST   /rooms/:id/reveal   {passwordHash} -> {ok:true,code} | 403 {ok:false,error:"bad password"}
//   DELETE /rooms/:id          header X-Room-Secret: <secret> -> {ok:true}
//
// Run it behind the same Nginx that serves the website and point the launcher at
// https://<host>/lobby (see scripts/lobby/nginx.conf.example).

import http from "node:http";
import crypto from "node:crypto";

const PORT = Number(process.argv[2] ?? process.env.PORT ?? 8787);
const MAX_TTL = 3600;
const MAX_ROOMS = 500;
const MAX_NAME = 48;
const MAX_NOTICE = 200;
const MAX_TAGS = 5;

/** @type {Map<string, {room: any, secret: string, expires: number}>} */
const rooms = new Map();

const json = (res, status, payload) => {
  const body = JSON.stringify(payload);
  res.writeHead(status, {
    "content-type": "application/json; charset=utf-8",
    "content-length": Buffer.byteLength(body),
    "cache-control": "no-store",
  });
  res.end(body);
};

const readBody = (req, limit = 16 * 1024) =>
  new Promise((resolve, reject) => {
    let size = 0;
    const chunks = [];
    req.on("data", (chunk) => {
      size += chunk.length;
      if (size > limit) {
        reject(new Error("body too large"));
        req.destroy();
        return;
      }
      chunks.push(chunk);
    });
    req.on("end", () => {
      try {
        resolve(chunks.length ? JSON.parse(Buffer.concat(chunks).toString("utf8")) : {});
      } catch {
        reject(new Error("invalid json"));
      }
    });
    req.on("error", reject);
  });

const clean = (value, max) => (typeof value === "string" ? value.replace(/[\u0000-\u001f]/g, " ").trim().slice(0, max) : "");

const roomCode = (value) => {
  const code = clean(value, 32);
  return /^U\/[0-9A-Z]{4}(-[0-9A-Z]{4}){3}$/i.test(code) ? code : "";
};

const hash = (value) => (typeof value === "string" ? value.toLowerCase() : "");

function prune() {
  const now = Date.now();
  for (const [id, entry] of rooms) {
    if (entry.expires <= now) rooms.delete(id);
  }
}

const view = (id, entry) => ({
  id,
  name: entry.room.name,
  notice: entry.room.notice,
  tags: entry.room.tags,
  players: entry.room.players,
  maxPlayers: entry.room.maxPlayers,
  version: entry.room.version,
  hasPassword: entry.room.passwordHash.length > 0,
  // the code itself only travels to someone who proved they know the password
  code: entry.room.passwordHash.length > 0 ? "" : entry.room.code,
});

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url ?? "/", "http://localhost");
  const path = url.pathname.replace(/\/+$/, "");

  try {
    if (req.method === "GET" && path === "/rooms") {
      prune();
      const list = [...rooms.entries()].map(([id, entry]) => view(id, entry));
      list.sort((a, b) => b.players - a.players);
      return json(res, 200, { rooms: list });
    }

    if (req.method === "POST" && path === "/rooms") {
      prune();
      const body = await readBody(req);
      const code = roomCode(body.code);
      const secret = clean(body.secret, 64);
      if (!code || secret.length < 16) return json(res, 400, { ok: false, error: "bad room" });
      if (rooms.size >= MAX_ROOMS) return json(res, 429, { ok: false, error: "lobby is full" });

      const ttl = Math.min(MAX_TTL, Math.max(60, Number(body.ttl) || 600));
      const tags = Array.isArray(body.tags)
        ? body.tags.slice(0, MAX_TAGS).map((tag) => clean(tag, 16)).filter(Boolean)
        : [];

      const room = {
        code,
        name: clean(body.name, MAX_NAME) || "未命名房间",
        notice: clean(body.notice, MAX_NOTICE),
        tags,
        players: Math.max(0, Math.min(100, Number(body.players) || 0)),
        maxPlayers: Math.max(0, Math.min(100, Number(body.maxPlayers) || 0)),
        version: clean(body.version, 32),
        passwordHash: hash(body.passwordHash),
      };

      // the same secret refreshes the same listing instead of piling up duplicates
      let id = [...rooms.entries()].find(([, entry]) => entry.secret === secret)?.[0];
      if (!id) id = crypto.randomBytes(8).toString("hex");
      rooms.set(id, { room, secret, expires: Date.now() + ttl * 1000 });
      return json(res, 200, { ok: true, id });
    }

    const reveal = path.match(/^\/rooms\/([0-9a-f]{16})\/reveal$/);
    if (req.method === "POST" && reveal) {
      prune();
      const entry = rooms.get(reveal[1]);
      if (!entry) return json(res, 404, { ok: false, error: "room is gone" });
      const body = await readBody(req);
      if (entry.room.passwordHash.length > 0 && hash(body.passwordHash) !== entry.room.passwordHash) {
        return json(res, 403, { ok: false, error: "bad password" });
      }
      return json(res, 200, { ok: true, code: entry.room.code });
    }

    const remove = path.match(/^\/rooms\/([0-9a-f]{16})$/);
    if (req.method === "DELETE" && remove) {
      const entry = rooms.get(remove[1]);
      if (!entry) return json(res, 404, { ok: false, error: "room is gone" });
      if (req.headers["x-room-secret"] !== entry.secret) return json(res, 403, { ok: false, error: "bad secret" });
      rooms.delete(remove[1]);
      return json(res, 200, { ok: true });
    }

    return json(res, 404, { ok: false, error: "not found" });
  } catch (error) {
    return json(res, 400, { ok: false, error: String(error?.message ?? error) });
  }
});

server.listen(PORT, () => {
  console.log(`SalmonLauncher lobby listening on :${PORT}`);
});
