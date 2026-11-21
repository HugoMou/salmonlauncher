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
    public const string Version = "2.0.16";

    public static IReadOnlyList<WhatsNewItem> Items { get; } =
    [
        new("\uE896",
            Loc.T("整合包一键导入"),
            Loc.T("「实例 → 模组管理」新增「导入整合包」：直接选 .mrpack 或 .zip，启动器会读取包内清单，把模组、配置、存档一次放到位，并逐文件校验；不需要手动解压再一个个导入。")),
        new("\uE7B8",
            Loc.T("加载器不用先装原版"),
            Loc.T("在「加载器选择」里直接点 Fabric / Quilt / Forge / NeoForge 即可：启动器会自动补齐缺少的原版文件，再生成可启动的加载器实例，不用先绕去装一遍原版。")),
        new("\uE768",
            Loc.T("整合包自带实例"),
            Loc.T("导入时如果整合包要求的版本或加载器与当前实例不一致，可以选择自动创建对应实例——原版下载、加载器合并、模组导入一次完成，导入完即可启动。")),
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
