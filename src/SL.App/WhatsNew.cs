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
    public const string Version = "2.0.18";

    public static IReadOnlyList<WhatsNewItem> Items { get; } =
    [
        new("\uE960",
            Loc.T("整合包一键导入并启动"),
            Loc.T("在「整合包仓库」点下载后不再只是丢文件：启动器会读清单、必要时自动建对应实例并导入，完成后直接问你要不要立即开玩。")),
        new("\uE8B9",
            Loc.T("资源包与光影缩略图"),
            Loc.T("资源包、光影与存档列表会直接显示包自带的 pack.png / icon.png，没有图片时回退到字形图标。")),
        new("\uE74E",
            Loc.T("备份恢复更稳"),
            Loc.T("修复从 zip 恢复到全新实例时因目录不存在而报错的问题；临时目录不可写时会自动回退到数据目录。")),
        new("\uE7BA",
            Loc.T("可离线自检"),
            Loc.T("仓库新增 31 项自检（Java 版本推断、模组依赖检查、实例 zip 往返），发版本前可一键验证核心逻辑。")),
        new("\uE74C",
            Loc.T("清理遗留产物"),
            Loc.T("删除录制与调试阶段遗留的散落图片与临时脚本，仓库体积和查找成本都降下来了。")),
    ];

    /// <summary>
    /// 是否需要弹出更新公告。
    /// 只有「已经记录过一个旧版本」且与当前版本不同时才弹，
    /// 即：升级后弹一次，之后永远不再弹；全新安装不弹（首次使用已有引导与条款）。
    /// </summary>
    public static bool ShouldShow(string lastSeenVersion)
    {
        if (string.IsNullOrWhiteSpace(lastSeenVersion))
            return false;

        return !string.Equals(lastSeenVersion.Trim(), Version, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 记下当前版本已处理；之后同一版本不会再弹公告。
    /// 首次安装也会写入，用于区分「首次使用」与「升级」。
    /// </summary>
    public static void MarkSeen()
        => AppServices.Settings.Update(c => c.LastSeenVersion = Version);

    /// <summary>当前版本是否尚未记录（即首次安装）。</summary>
    public static bool IsFirstInstall(string lastSeenVersion)
        => string.IsNullOrWhiteSpace(lastSeenVersion);

    /// <summary>Version whose notes the user has already seen (empty when none).</summary>
    public static string LastSeen => AppServices.Settings.Current.LastSeenVersion;
}
