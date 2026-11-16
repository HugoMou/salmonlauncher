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
using SL.App.Dialogs;
using SL.App.Infrastructure;
using SL.App.Localization;

namespace SL.App;

/// <summary>
/// Release notes shown once per version. Keep entries user-facing: what changed, not how it was coded.
/// </summary>
public static class WhatsNew
{
    /// <summary>Version these notes describe; compared with the stored "last seen" version.</summary>
    public const string Version = "2.0.15";

    public static IReadOnlyList<WhatsNewItem> Items { get; } =
    [
        new("\uE895",
            Loc.T("更新提示不再反复弹出"),
            Loc.T("启动时的「发现新版本」提示现在只提醒一次：同一版本提醒过后不再重复，安装新版后才会提示下一个新版本。")),
    ];

    /// <summary>
    /// Whether the announcement should be shown for the running version.
    /// An empty value means the user has never seen one, in which case it is shown once.
    /// </summary>
    public static bool ShouldShow(string lastSeenVersion)
        => !string.Equals(lastSeenVersion, Version, StringComparison.OrdinalIgnoreCase);

    /// <summary>Remember that this version's notes have been shown, so they never repeat.</summary>
    public static void MarkSeen()
        => AppServices.Settings.Update(c => c.LastSeenVersion = Version);

    /// <summary>Version whose notes the user has already seen (empty when none).</summary>
    public static string LastSeen => AppServices.Settings.Current.LastSeenVersion;
}
