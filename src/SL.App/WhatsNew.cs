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
    public const string Version = "2.0.3";

    public static IReadOnlyList<WhatsNewItem> Items { get; } =
    [
        new("\uE74D",
            Loc.T("未处理的错误不再让启动器退出"),
            Loc.T("以前后台任务或界面事件里出现未预期错误时，启动器会直接消失；现在会记录下来、提示一次，并继续运行。")),
        new("\uE7BA",
            Loc.T("补齐易失败操作的错误处理"),
            Loc.T("皮肤预览、Java 扫描、打开登录链接、联机说明与更新对话框等操作失败时不再崩溃，改为记录日志并给出提示。")),
        new("\uE71C",
            Loc.T("修复模组启停导致的崩溃"),
            Loc.T("在模组管理里开关模组会重建列表，导致切换页面时崩溃；现在改为就地更新那一行，不再重建。")),
        new("\uE7C3",
            Loc.T("修复模组无法启停"),
            Loc.T("上次切换被中断后残留的同名文件会让模组一直报「目标已存在」；现在会自动清理残留，正常启用或停用。")),
        new("\uE895",
            Loc.T("可以在设置里自我更新"),
            Loc.T("「外观与设置 → 软件更新」会从 GitHub 发布页检查新版本、下载安装包并直接安装；升级只替换程序文件，设置、账户、实例与存档都保留。")),
        new("\uE7BA",
            Loc.T("启动时自动检查更新"),
            Loc.T("可在同一处关闭自动检查；有可用更新时首页会提示，不会在后台静默安装。")),
        new("\uE716",
            Loc.T("联机由随包的 Campfire 提供"),
            Loc.T("联机页启动随附的 Campfire：它负责把世界开放到局域网并生成邀请码。启动器可一键检查并更新 Campfire，多源加速并校验 SHA-256。")),
        new("\uE896",
            Loc.T("下载更快、更稳"),
            Loc.T("多连接分段下载并支持镜像回退，带实时速度显示与随时取消；「镜像优先」设置对启动修补同样生效。")),
        new("\uE8A5",
            Loc.T("协议与版权说明完整"),
            Loc.T("用户协议与免责声明共 13 章，包含第三方组件版权、再分发义务与数据保留说明。")),
        new("\uE72A",
            Loc.T("本版本为 2.0.1"),
            Loc.T("不提供便携版；安装包会保留已有用户数据。")),
    ];

    /// <summary>Whether the announcement should be shown for the running version.</summary>
    public static bool ShouldShow(string lastSeenVersion)
        => !string.Equals(lastSeenVersion, Version, StringComparison.OrdinalIgnoreCase);

    public static void MarkSeen()
        => AppServices.Settings.Update(c => c.LastSeenVersion = Version);
}
