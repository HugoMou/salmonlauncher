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
    public const string Version = "2.0.13";

    public static IReadOnlyList<WhatsNewItem> Items { get; } =
    [
        new("\uE790",
            Loc.T("窗口背景默认改为普通样式"),
            Loc.T("云母（Mica）半透明背景不再默认开启，默认使用普通配色背景；如需可在「外观与设置 → 外观」中手动开启。套用壁纸时仍以壁纸为准。")),
        new("\uE713",
            Loc.T("新增云母材质开关"),
            Loc.T("在外观设置中可选择是否启用 Windows 11 云母材质，旧版系统会自动回退为普通背景。")),
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
