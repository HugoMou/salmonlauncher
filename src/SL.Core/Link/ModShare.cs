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
using System.Security.Cryptography;
using SL.Core.Link.Room;

namespace SL.Core.Link;

/// <summary>
/// The mods of one game instance, described to guests and served file by file. A room uses this so that
/// everybody plays with exactly the same mod set instead of comparing folders by hand.
/// </summary>
public sealed class ModShare
{
    private readonly Dictionary<string, string> _pathBySha1 = new(StringComparer.OrdinalIgnoreCase);

    private ModShare(ModList list, Dictionary<string, string> pathBySha1)
    {
        List = list;
        _pathBySha1 = pathBySha1;
    }

    /// <summary>What guests see: game version, loader and one entry per mod.</summary>
    public ModList List { get; }

    /// <summary>Whether there is anything to serve.</summary>
    public bool HasMods => List.Mods.Count > 0;

    /// <summary>Describes (and fingerprints) every jar in <paramref name="directory"/>.</summary>
    public static ModShare FromDirectory(string? directory, string gameVersion, string loader)
    {
        var entries = new List<ModEntry>();
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.jar", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.Length == 0 || info.Length > 200L * 1024 * 1024)
                        continue;

                    string sha1 = Convert.ToHexStringLower(SHA1.HashData(File.ReadAllBytes(file)));
                    entries.Add(new ModEntry(info.Name, sha1, info.Length));
                    paths[sha1] = file;
                }
                catch (IOException ex)
                {
                    LinkLog.Warn($"读取模组 {Path.GetFileName(file)} 失败:{ex.Message}");
                }
            }
        }
        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return new ModShare(new ModList(gameVersion, loader, entries), paths);
    }

    /// <summary>Path of the file behind a SHA-1, or <c>null</c> when this instance does not have it.</summary>
    public string? PathFor(string sha1) => _pathBySha1.TryGetValue(sha1, out string? path) ? path : null;
}
