# SLCompanion

Companion Fabric mod for **SalmonLauncher**. It does three things:

1. **Opens singleplayer worlds to LAN automatically** once the world has finished loading, and
   reports the port the integrated server actually bound.
2. **Publishes a small JSON status file** so the launcher can show the room state.
3. **Mirrors an optional "room" file** the launcher writes into the tab list header/footer, so
   everyone on the LAN sees the room name, host and expected players.

Target: Minecraft **1.21.1**, Fabric Loader **0.16.x**, Fabric API, Java **21**.

## Launcher side: JVM arguments

The launcher must pass these on the game command line (absolute paths):

```
-Dsl.link.status=C:\path\to\room-status.json
-Dsl.link.room=C:\path\to\room-info.json      (optional)
```

| Property | Required | Meaning |
| --- | --- | --- |
| `sl.link.status` | yes | File the mod writes. Parent directories are created. |
| `sl.link.room` | no | File the launcher writes. If it does not exist the mod does nothing. |

The mod also needs Fabric API installed next to it in the same `mods/` folder.

## Status file written by the mod

World open and LAN bound:

```json
{"port":51234,"motd":"world name","players":2,"maxPlayers":8,"ready":true,"updatedAt":"2026-11-07T08:30:00Z"}
```

No world open:

```json
{"ready":false}
```

`port` is `0` and `ready` is `false` while the world is loading and LAN has not been bound yet.
The file is rewritten on world load, on player join/leave, and every 5 seconds while a world is
open. Writes are atomic (temp file + rename, with a direct-write fallback).

## Room file read by the mod

```json
{"roomName":"My World","hostName":"Alice","players":["Alice","Bob"]}
```

Content is appended to the tab list header (room + host) and footer (players). It is re-sent when
the file changes and when a player joins. Missing/unreadable/malformed file: the tab list is left
untouched. Read every second, delivered through vanilla's `PlayerListHeaderS2CPacket`.

## How the LAN opening works

* `MinecraftServerMixin` injects at `RETURN` of `MinecraftServer#loadWorld()` and marks the world as
  loaded. This is a mixin on a stable, long-lived method (verified against yarn `1.21.1+build.3`:
  `method_3735 ()V`), not a deep internal hook.
* The client tick handler then calls `MinecraftServer#openToLan(GameMode, boolean, int)` — the same
  routine the vanilla "Open to LAN" button uses — on the client thread, passing port `0` so the OS
  picks a free port, then reads the real port from `MinecraftServer#getServerPort()`.
* If binding fails, it retries every 2 s and, after three attempts, also tries fixed ports
  51234-51240.
* The mixin is only a trigger; the tick loop also starts LAN if the server is present but the hook
  never fired, so a future mapping change cannot leave the feature dead.

`openToLan`, `getServerPort`, `getSaveProperties().getLevelName()` and `PlayerManager` count getters
are all resolved at compile time against the real 1.21.1 mappings; the generated
`slcompanion-refmap.json` confirms the mixin target:

```json
"loadWorld": "Lnet/minecraft/server/MinecraftServer;method_3735()V"
```

## Building

Requires JDK 21 (`JAVA_HOME`) and network access to `maven.fabricmc.net`, `repo1.maven.org`,
`plugins.gradle.org`.

```
gradlew.bat build
```

Output: `build/libs/slcompanion-1.0.0.jar`.

`gradle/wrapper/gradle-wrapper.properties` points at a Gradle 8.14.3 mirror; replace
`distributionUrl` with `https\://services.gradle.org/distributions/gradle-8.14.3-bin.zip` if you
prefer the canonical host.

## Notes

* `environment: "*"` so the common status/room code also loads on a dedicated server, but the LAN
  mixin lives in the `client` section of `slcompanion.mixins.json` and is only applied client side.
* Nothing is written when `sl.link.status` is unset; the mod stays inert.
* License: GPL-3.0-or-later (matches SalmonLauncher).
