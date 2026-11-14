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
    public const string Version = "2.0.12";

    public static IReadOnlyList<WhatsNewItem> Items { get; } =
    [
        new("\uE721",
            Loc.T("仓库页新增热门推荐"),
            Loc.T("模组、光影、资源包仓库在搜索框为空时会按下载量展示热门内容，并支持「换一批」查看更多；输入关键词后切换为搜索结果，清空搜索框可回到推荐。")),
        new("\uE7BA",
            Loc.T("修复无实例时打开概览会卡死"),
            Loc.T("未安装任何实例时进入「实例概览」会触发界面线程死循环并卡住启动器；现已修复，新用户可正常看到空状态。")),
        new("\uE8B7",
            Loc.T("迁移数据支持选择文件夹"),
            Loc.T("欢迎页的迁移步骤新增「选择数据文件夹」，可手动指定旧启动器或 .minecraft 数据目录进行识别与导入。")),
        new("\uE7C4",
            Loc.T("修复欢迎页布局"),
            Loc.T("欢迎页顶部原本多出一条空白横条，把标题往下顶；现在标题直接贴顶，内容整体上移。")),
        new("\uE73E",
            Loc.T("修复欢迎页最后一步"),
            Loc.T("第 5 步原本仍显示「下一步」，点击后进入一个没有明确出口的完成页，按钮还会与「跳过」重叠。现在最后一步直接显示「开始使用」，点一次即可完成引导。")),
        new("\uE74E",
            Loc.T("新增实例健康检查"),
            Loc.T("在实例概览点「检查实例」，会检查核心文件是否完整、是否缺少 Fabric API、是否存在重复模组，并给出处理建议。")),
        new("\uE8B7",
            Loc.T("新增还原备份"),
            Loc.T("存档页新增「还原备份」：从历史备份中选一份覆盖当前存档，原存档会先移到 backups 目录，随时可以找回。")),
        new("\uE710",
            Loc.T("新增复制实例"),
            Loc.T("实例概览可一键复制当前实例的模组、存档与配置；游戏版本文件共享，不会重复占用磁盘。")),
        new("\uE774",
            Loc.T("补齐英文与日文翻译"),
            Loc.T("此前约 110 条界面文案（条款正文、联机说明、更新公告等）在英文与日文界面下仍显示中文，现已全部补齐。")),
        new("\uE8B7",
            Loc.T("新增「打开文件夹」"),
            Loc.T("启动面板的实例选择旁可直接打开当前实例的文件夹，方便查看或手动管理文件。")),
        new("\uE7BA",
            Loc.T("启动失败可直接跳转诊断"),
            Loc.T("启动失败时会询问是否打开崩溃诊断，不必再自己找入口。")),
        new("\uE74D",
            Loc.T("全面加固事件处理"),
            Loc.T("所有页面操作都补上了错误保护，异常会记录到日志并提示，不会让启动器退出；清除数据中途出错也会明确告知。")),
        new("\uE895",
            Loc.T("更新公告只弹一次"),
            Loc.T("此前更新公告要等你关掉对话框才记录，未关掉就会每次启动重复弹出；现在启动时先记录再显示，只弹一次。想再看可在「关于」页点「查看本次更新」。")),
        new("\uE8FD",
            Loc.T("列表不再显示空徽章"),
            Loc.T("版本列表与任务卡片此前会为空内容画出彩色徽章框；现在没有内容时不显示徽章。")),
        new("\uE713",
            Loc.T("任务卡片只显示可用的操作"),
            Loc.T("暂停、继续、重试、取消四个按钮此前始终并排显示、多数点不动；现在只显示当前状态下能用的那些。")),
        new("\uE7BA",
            Loc.T("没有实例时启动按钮会正确置灰"),
            Loc.T("此前没有可用实例时按钮仍可点击、只有点下去才弹出提示；现在会直接禁用，并在按钮上说明原因。")),
        new("\uE790",
            Loc.T("支持 Mica 半透明背景"),
            Loc.T("未套用壁纸时，窗口背景改用 Windows 11 的 Mica 材质，能透出桌面；套用壁纸时仍以壁纸为准，旧版系统会自动回退为纯色。")),
        new("\uE7BA",
            Loc.T("协议更新后会再次确认"),
            Loc.T("以前只要同意过一次就不再提示；现在会校验条款版本，协议改版后会再次请你确认新内容。")),
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
