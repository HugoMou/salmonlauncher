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

### What was not ported, and why

Campfire's `Link/Room/*` (room service protocol, client, server) and `Link/Minecraft/*` (game proxy, Java
agent, overlay, LAN announcer) depend on types from namespaces `Campfire.Mods` (`ModHost`, `ModList`,
`VoiceOffer`) and `Campfire.Link.Lobby`, plus the embedded Java agent jars under `src/java-agent/dist/`.
Those namespaces are referenced by the published sources but are **not present** in the published
repository at `main` (92 `.cs` files, no `Mods/` or `Lobby/` directory), so those files cannot be copied
and compiled as they stand. SalmonLauncher keeps its own room layer and its own companion mod
(`mod/`, Fabric) for the in-game part.

Campfire's WinUI user interface, launcher repair, map browser, telemetry and updater are not part of this
port.
