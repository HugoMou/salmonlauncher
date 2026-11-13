# Third-party notices

SalmonLauncher is distributed under the GNU General Public License, version 3 or later (GPL-3.0-or-later).
The component below is a third-party work; this statement records what is shipped, from where, and under
which licence, as the GPL requires.

---

## Campfire — https://github.com/Xocio/Campfire

The multiplayer feature of this launcher **is Campfire**: the upstream release binary is shipped unchanged
and started from the 联机 page as a separate program.

- Program: `Campfire.exe`, version **v1.1.32**, SHA-256
  `35acc188093bd28bf844d622d9e3c4db86aa8d438929d153b4c118cb9ff46f6e`
- Files: `src/SL.App/campfire/` — `Campfire.exe`, `LICENSE` (GPL-3.0), `SOURCE-OFFER.txt`
- Licence: GNU General Public License v3.0 (without an "or later" clause)
- Source: https://github.com/Xocio/Campfire (tag `v1.1.32`)
- The launcher does not modify, repack or patch it, and takes no part in its network traffic.
- The 联机 page can check the project's release feed and download a newer official `Campfire.exe` into
  the same folder; that copy is also unmodified and remains under GPL-3.0. Downloads are verified against
  the SHA-256 GitHub publishes for the asset.

`SOURCE-OFFER.txt` in that folder carries the same facts for the end user, including where the complete
corresponding source can be obtained. A distribution of this launcher that contains that folder is
distributed as a whole under **GPL-3.0**, while the files written for SalmonLauncher remain available under
GPL-3.0-or-later. The full licence text is `LICENSE` at the repository root, or
https://www.gnu.org/licenses/gpl-3.0.html.

### Bundling and redistribution duties

Because `Campfire.exe` is GPL-3.0 and is shipped inside the installer, **any build that includes it must be
distributed as a whole under GPL-3.0**. This applies to SalmonLauncher v2.0.0 and every later build that keeps
the `campfire/` folder. When redistributing:

- keep `campfire/LICENSE`, `campfire/SOURCE-OFFER.txt` and this file together with the program;
- keep the version and SHA-256 above accurate for the copy actually shipped (or note the newer copy);
- make the complete corresponding source available as described in `SOURCE-OFFER.txt`;
- do not add restrictions on top of GPL-3.0 for the combined work.

The launcher's self-updater downloads only the official installer from this project's own release page and
verifies it against the SHA-256 published there; it never fetches third-party binaries.

### Why the release binary is shipped instead of a rebuild

The release binary is shipped unmodified rather than rebuilt locally so that its version and SHA-256
match the upstream release exactly. The project's repository publishes the source for tag `v1.1.32`, so
the complete corresponding source remains available as described above.

Newer copies can also be downloaded through this launcher's 联机 page; such copies remain under the
same licence, and the version and checksum above describe the copy this build ships.

### Not included

Campfire's own user interface is not embedded in this launcher's window: the launcher starts the program
as a separate application. Nothing else from the Campfire project is redistributed.
