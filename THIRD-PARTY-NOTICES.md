# Third-party notices

SalmonLauncher is distributed under the GNU General Public License, version 3 or later (GPL-3.0-or-later).
The components below are third-party works; the statements here record what was taken, from where, and under
which licence, as the GPL requires.

---

## Campfire — https://github.com/Xocio/Campfire

- Licence: GNU General Public License v3.0 (GPL-3.0, without an "or later" clause).
- Copyright: the Campfire contributors.
- Source: branch `main`, retrieved 2026-11-08.

### Licence compatibility

Campfire is GPL-3.0 and this project is GPL-3.0-or-later, so Campfire code may be combined with it. Because
the Campfire part cannot be relicensed to "any later version", **any distribution that contains these files is
distributed as a whole under GPL-3.0**, while the files written for SalmonLauncher remain available under
GPL-3.0-or-later. The full licence text is `LICENSE` at the repository root, or
https://www.gnu.org/licenses/gpl-3.0.html.

Every ported file carries a header naming Campfire, the retrieval date and the changes made. The original
files in the upstream repository carry no per-file copyright header; the header text added here is the
attribution this project is asked to preserve.

### Ported files

| File in this repository | Origin in Campfire | Kind |
| --- | --- | --- |
| `src/SL.Core/Link/Json.cs` | `src/Campfire/Link/Json.cs` | verbatim (namespace) |
| `src/SL.Core/Link/Hex.cs` | `src/Campfire/Hex.cs` | verbatim (namespace) |
| `src/SL.Core/Link/Runtime/EasyTierConfig.cs` | `src/Campfire/Link/Runtime/EasyTierConfig.cs` | adapted |
| `src/SL.Core/Link/Runtime/EasyTierInstance.cs` | `src/Campfire/Link/Runtime/EasyTierInstance.cs` | adapted |
| `src/SL.Core/Link/Runtime/EasyTierPackage.cs` | `src/Campfire/Link/Runtime/EasyTierPackage.cs` | adapted |
| `src/SL.Core/Link/Runtime/EasyTierPeer.cs` | `src/Campfire/Link/Runtime/EasyTierPeer.cs` | adapted |
| `src/SL.Core/Link/Runtime/ProgressStream.cs` | `src/Campfire/Link/Runtime/ProgressStream.cs` | verbatim (namespace) |
| `src/SL.Core/Link/Runtime/RemoteZip.cs` | `src/Campfire/Link/Runtime/RemoteZip.cs` | verbatim (namespace) |
| `src/SL.Core/Link/Runtime/ChildProcessJob.cs` | `src/Campfire/Link/Runtime/ChildProcessJob.cs` | verbatim (namespace) |
| `src/SL.Core/Link/Room/RoomFraming.cs` | `src/Campfire/Link/Room/RoomFraming.cs` | adapted |
| `src/SL.Core/Link/Room/RoomProtocol.cs` | `src/Campfire/Link/Room/RoomProtocol.cs` | adapted |
| `src/SL.Core/Link/Room/RoomClient.cs` | `src/Campfire/Link/Room/RoomClient.cs` | adapted |
| `src/SL.Core/Link/Room/RoomServer.cs` | `src/Campfire/Link/Room/RoomServer.cs` | adapted |
| `src/SL.Core/Link/Room/PlayerProfile.cs` | `src/Campfire/Link/Room/PlayerProfile.cs` | adapted |

### Changes made to the ported files

- Namespace `Campfire.*` → `SL.Core.Link.*`, and the types the launcher calls are `public`.
- `AppSettings.DataDirectory` → `LinkSettings.DataDirectory` (this project's data directory).
- Two user-facing error messages are Chinese instead of localisation keys
  (`Link.Error.DownloadUnreachable`, `Link.Error.DownloadFailed`).
- `EasyTierConfig` gained a `NoTun` flag (default off here). Upstream always runs EasyTier without a TUN
  device and reaches peers through its port-forward/proxy layer; this launcher reaches the room service by
  virtual address, so the adapter stays enabled.
- `EasyTierInstance.StopLeftovers` used `LauncherProcess.TryGetImagePath` from Campfire's launcher-repair
  code, which is not ported; a local `TryGetImagePath` reads `Process.MainModule.FileName` instead.
- Nullable reference annotations are disabled inside the ported files: upstream compiles without nullable
  analysis, and the port does not rewrite the code to satisfy it.
- The room layer's wire kinds are renamed from `campfire:*` to `sl:*` (both ends of a room are this launcher),
  matching the prefixes already used here, and `RoomServer`/`RoomClient` are `public` so the test harness can
  drive them.
- `RoomServer` gained `UpdateMinecraftPort`, because this launcher lets a host share the code before entering
  a world, while upstream knows the port when the room opens.
- `PlayerProfile.CreateLocal` reads this project's settings and account instead of Campfire's account store;
  the premium UUID still only travels when skin sharing is enabled. `LocalVendor` names SalmonLauncher.
- `LinkSettings` gained static `ShareMods` / `ShowSkins` / `ShareVersion` conveniences that the ported room
  service reads.

### Types written for this project, matching the calls the ported code makes

Campfire's published sources reference a `Campfire.Mods` namespace and a `RoomInfo`/`Blocklist` that are not
in the repository, so these are written here rather than copied:

| File in this repository | Purpose |
| --- | --- |
| `src/SL.Core/Link/Mods/Mods.cs` | `ModFile`, `ModList`, `VoiceOffer`, `ModHost` (list + chunked file serving) |
| `src/SL.Core/Link/Room/RoomInfo.cs` | room name, announcement and player limit |
| `src/SL.Core/Link/Blocklist.cs` | players a host removed, remembered across sessions |

`src/SL.Core/Link/LinkSession.cs` is this project's own orchestrator; it now drives the ported room service
and network over local port forwards (guest reaches the room through `AddPortForwardAsync`, the game joins
`127.0.0.1`), which is why neither side needs a virtual adapter or elevation.

### What was not ported, and why

- `Link/Minecraft/*` (game proxy, Java agent, overlay, LAN announcer, player heads) is not ported. It is tied
  to Windows Presentation Foundation media types, to Campfire's embedded Java agent jars under
  `src/java-agent/dist/`, and to its localisation layer. SalmonLauncher keeps its own Fabric companion mod
  (`mod/`) for opening worlds to LAN and showing room information in game.
- The lobby (`Online/*`) is Campfire's own service contract; SalmonLauncher talks to its own small lobby
  (`scripts/lobby/`).

Campfire's WinUI user interface, launcher repair, map browser, telemetry and updater are not part of this
port.
