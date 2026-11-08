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
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SL.App.Dialogs;
using SL.App.Infrastructure;
using SL.App.Localization;
using SL.App.Notify;
using SL.Core.Logging;
using Windows.ApplicationModel.DataTransfer;

namespace SL.App.Pages;

/// <summary>
/// Multiplayer is Campfire: the launcher ships Campfire's release unchanged (GPL-3.0) and starts it.
/// Campfire takes over the game itself, opens singleplayer worlds to LAN and hands out the invite code.
/// </summary>
public sealed partial class MultiplayerPage : Page
{
    /// <summary>Version of the bundled release, shown in the page and in THIRD-PARTY-NOTICES.md.</summary>
    public const string BundledVersion = "v1.1.31";

    /// <summary>SHA-256 of the file this build ships, so a replaced binary is visible.</summary>
    public const string BundledSha256 = "39086802e89880d7793e0952434ce648945dd4ef1d8276db5ffdb5d42d419f15";

    private readonly DispatcherQueue _ui = DispatcherQueue.GetForCurrentThread();

    public MultiplayerPage()
    {
        InitializeComponent();
        PageScroll.SizeChanged += (_, _) => FitToViewport();
        Loaded += (_, _) => FitToViewport();
    }

    private void FitToViewport()
    {
        double width = ActualWidth - 72;
        if (width > 320)
            ContentRoot.Width = width;
    }

    /// <summary>Where the bundled program lives: next to the launcher executable.</summary>
    private static string CampfirePath => Path.Combine(AppContext.BaseDirectory, "campfire", "Campfire.exe");

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        bool present = File.Exists(CampfirePath);
        StatusText.Text = present
            ? Loc.T("联机由内置的 Campfire 提供,点右上角启动即可")
            : Loc.T("没有找到内置的 Campfire,联机暂不可用");
        VersionText.Text = $"{Loc.T("版本")} {BundledVersion} · SHA-256 {BundledSha256[..16]}…";
        PathText.Text = CampfirePath;
        LicenseText.Text = Loc.T("许可证:GPL-3.0 · 源码:https://github.com/Xocio/Campfire");
        MissingCard.Visibility = present ? Visibility.Collapsed : Visibility.Visible;
        MissingText.Text = string.Format(
            Loc.T("应位于 {0}。请重新安装启动器,或从上面的源码地址获取并放入该目录。"), CampfirePath);
        LaunchButton.IsEnabled = present;
    }

    private async void OnLaunchCampfire(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(CampfirePath))
            {
                await AppServices.Dialogs.AlertAsync(Loc.T("找不到 Campfire"), CampfirePath, MsgMark.Warning);
                return;
            }

            // already running? bring that copy forward instead of starting a second one
            var running = Process.GetProcessesByName("Campfire").FirstOrDefault(p => p.MainWindowHandle != 0);
            if (running is not null)
            {
                SetForegroundWindow(running.MainWindowHandle);
                SetDetail(Loc.T("Campfire 已在运行"));
                return;
            }

            Process.Start(new ProcessStartInfo(CampfirePath)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(CampfirePath) ?? AppContext.BaseDirectory,
            });
            SetDetail(Loc.T("已启动 Campfire,在那里开房或加入房间"));
            AppServices.Notify.Publish(NotifyKind.Success, Loc.T("已启动 Campfire"));
        }
        catch (Exception ex)
        {
            AppLog.Error("Multiplayer", ex);
            SetDetail(Loc.T("启动 Campfire 失败:") + ex.Message);
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("启动 Campfire 失败:") + ex.Message);
        }
    }

    private void OnCopyPath(object sender, RoutedEventArgs e)
    {
        try
        {
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            package.SetText(CampfirePath);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            SetDetail(Loc.T("路径已复制"));
        }
        catch (Exception)
        {
            SetDetail(Loc.T("复制失败"));
        }
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            string directory = Path.GetDirectoryName(CampfirePath) ?? AppContext.BaseDirectory;
            if (Directory.Exists(directory))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetDetail(Loc.T("打开文件夹失败:") + ex.Message);
        }
    }

    private async void OnShowHelp(object sender, RoutedEventArgs e)
    {
        await AppServices.Dialogs.AlertAsync(Loc.T("联机说明"),
            Loc.T("1. 点「打开 Campfire」。\n2. 在 Campfire 里进入游戏,它会自动对局域网开放并生成邀请码。\n3. 把邀请码发给好友,好友在 Campfire 里粘贴即可加入。\n\n联机大厅、跨网联机与更新都由 Campfire 自己完成,启动器不介入其通信。"),
            MsgMark.Info);
    }

    private void SetDetail(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            DetailText.Text = "";
            DetailText.Visibility = Visibility.Collapsed;
            return;
        }
        DetailText.Text = text;
        DetailText.Visibility = Visibility.Visible;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
