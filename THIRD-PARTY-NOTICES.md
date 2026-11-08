# Third-party notices

SalmonLauncher is distributed under the GNU General Public License, version 3 or later (GPL-3.0-or-later).
The component below is a third-party work; this statement records what is shipped, from where, and under
which licence, as the GPL requires.

---

## Campfire — https://github.com/Xocio/Campfire

The multiplayer feature of this launcher **is Campfire**: the upstream release binary is shipped unchanged
and started from the 联机 page.

- Program: `Campfire.exe`, version **v1.1.31**, SHA-256
  `39086802e89880d7793e0952434ce648945dd4ef1d8276db5ffdb5d42d419f15`
- Files: `src/SL.App/campfire/` — `Campfire.exe`, `LICENSE` (GPL-3.0), `SOURCE-OFFER.txt`
- Licence: GNU General Public License v3.0 (without an "or later" clause)
- Source: https://github.com/Xocio/Campfire (tag `v1.1.31`)
- The launcher does not modify, repack or patch it, and takes no part in its network traffic.

`SOURCE-OFFER.txt` in that folder carries the same facts for the end user, including where the complete
corresponding source can be obtained. A distribution of this launcher that contains that folder is
distributed as a whole under **GPL-3.0**, while the files written for SalmonLauncher remain available under
GPL-3.0-or-later. The full licence text is `LICENSE` at the repository root, or
https://www.gnu.org/licenses/gpl-3.0.html.

### Why the release binary is shipped instead of a rebuild

Campfire's published repository does not contain every file its own sources reference: the namespaces
`Campfire.Mods` and `Campfire.Link.Lobby` are used by the published code but are absent from branch `main`
(92 `.cs` files, no `Mods/` or `Lobby/` folder), so the program cannot be rebuilt from that repository as it
stands. Shipping the upstream release unchanged, together with its licence and this notice, is the compliant
way to include it.

Campfire updates itself from its own release feed; an updated copy remains under the same licence, and the
version and checksum above describe the copy this build ships.

### Not included

Campfire's own user interface is not embedded in this launcher's window: the launcher starts the program as
a separate application. Nothing else from the Campfire project is redistributed.
