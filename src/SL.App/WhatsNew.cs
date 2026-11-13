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
    public const string Version = "2.0.0";

    public static IReadOnlyList<WhatsNewItem> Items { get; } =
    [
        new("\uE895",
            Loc.T("启动器可以在设置里自我更新"),
            Loc.T("「外观与设置 → 软件更新」会从 GitHub 发布页检查新版本、下载安装包并直接安装；升级只替换程序文件，设置、账户、实例与存档都保留。")),
        new("\uE7BA",
            Loc.T("启动时自动检查更新"),
            Loc.T("可在同一处关闭自动检查；有可用更新时首页会提示，不会在后台静默安装。")),
        new("\uE716",
            Loc.T("联机改用 Campfire"),
            Loc.T("联机页改为启动随附的 Campfire：它负责把世界开放到局域网并生成邀请码。启动器可一键检查并更新 Campfire，支持多源加速与 SHA-256 校验。")),
        new("\uE896",
            Loc.T("下载更快、更稳"),
            Loc.T("下载改为多连接分段并进行镜像回退，支持实时速度显示与随时取消；「镜像优先」设置现在对启动修补同样生效。")),
        new("\uE8A5",
            Loc.T("协议与版权说明更完整"),
            Loc.T("用户协议与免责声明已重写扩写，新增第三方组件版权、再分发义务与数据保留说明；关于页补充了组件与源码获取方式。")),
        new("\uE7FC",
            Loc.T("界面与提示更准确"),
            Loc.T("重做了联机页与设置页布局，减少重复点击；修正了动作路径、内存与磁盘占用、联机能力等与实际不符的说明。")),
        new("\uE72A",
            Loc.T("版本号升级到 2.0.0"),
            Loc.T("本次为大版本更新，包含上述功能与修复；此版本同样不提供便携版，安装包会保留已有用户数据。")),
    ];

    /// <summary>Whether the announcement should be shown for the running version.</summary>
    public static bool ShouldShow(string lastSeenVersion)
        => !string.Equals(lastSeenVersion, Version, StringComparison.OrdinalIgnoreCase);

    public static void MarkSeen()
        => AppServices.Settings.Update(c => c.LastSeenVersion = Version);
}
