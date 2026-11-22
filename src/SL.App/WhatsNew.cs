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
    public const string Version = "2.0.17";

    public static IReadOnlyList<WhatsNewItem> Items { get; } =
    [
        new("\uE790",
            Loc.T("界面全面重构"),
            Loc.T("启动面板改为卡片式布局：启动、账户、快捷入口与上手指南在一屏内全部可见，悬停与按下都有反馈。")),
        new("\uE8D7",
            Loc.T("云母材质与动效开关"),
            Loc.T("「外观与设置」新增「界面动效」开关；云母材质默认开启，不喜欢可一键关闭。")),
        new("\uE74E",
            Loc.T("实例克隆与备份"),
            Loc.T("「实例设置」新增克隆、导出备份与从备份恢复：做模组试验前先备份，出问题一键恢复。")),
        new("\uE960",
            Loc.T("整合包仓库"),
            Loc.T("资源里新增「整合包仓库」，直接按游戏版本检索 Modrinth 整合包。")),
        new("\uE943",
            Loc.T("Java 运行时自动检测"),
            Loc.T("实例设置里可看到检测到的 Java 列表，缺失时一键从 Adoptium 下载对应版本。")),
        new("\uE9D9",
            Loc.T("模组依赖检查"),
            Loc.T("「模组管理」新增「检查依赖」：静态读取 fabric.mod.json 与 mods.toml，提示缺失依赖与重复安装。")),
        new("\uE8A5",
            Loc.T("引导可跳过，协议已重写"),
            Loc.T("首次启动向导加入欢迎区与「跳过引导」；用户协议与免责声明升级到 3.0，重新提示确认。")),
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
