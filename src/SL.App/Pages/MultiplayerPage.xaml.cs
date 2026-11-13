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
using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SL.App.Dialogs;
using SL.App.Infrastructure;
using SL.App.Localization;
using SL.App.Notify;
using SL.App.Themes;
using SL.Core.Logging;
using Windows.ApplicationModel.DataTransfer;

namespace SL.App.Pages;

/// <summary>
/// Multiplayer is Campfire: the launcher ships Campfire's official release unchanged (GPL-3.0),
/// starts it as a separate program, and can fetch newer releases from the project's own release page.
/// Campfire takes over the game itself, opens singleplayer worlds to LAN and hands out the invite code.
/// </summary>
public sealed partial class MultiplayerPage : Page
{
    private readonly DispatcherQueue _ui = DispatcherQueue.GetForCurrentThread();

    private Process? _campfire;
    private MultiplayerState _state = MultiplayerState.NotInstalled;
    private CampfireRelease? _latest;
    private bool _busy;
    private bool _downloading;
    private CancellationTokenSource? _updateCts;

    private const int SW_RESTORE = 9;

    public MultiplayerPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AdoptExistingCampfire();
            RefreshUi();
        };
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        AdoptExistingCampfire();
        RefreshUi();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        // Campfire runs on its own: leaving this page never closes it.
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Stop an in-flight download; leaving the page should not keep writing files in the background.
        _updateCts?.Cancel();

        // Drop the event subscription so a cached page is not kept alive by a long-running process.
        if (_campfire is { } process)
        {
            process.Exited -= OnCampfireExited;
            process.Dispose();
            _campfire = null;
        }
    }

    // ---------------------------------------------------------- State and view

    private bool IsCampfireRunning()
    {
        try
        {
            return _campfire is { } process && !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private void AdoptExistingCampfire()
    {
        if (IsCampfireRunning())
            return;

        var existing = FindRunningCampfire();
        if (existing is null)
            return;

        AttachProcess(existing);
        _state = MultiplayerState.Running;
    }

    private static Process? FindRunningCampfire()
    {
        var expected = Path.GetFullPath(CampfireUpdater.ProgramPath);
        foreach (var process in Process.GetProcessesByName("Campfire"))
        {
            try
            {
                if (process.HasExited)
                {
                    process.Dispose();
                    continue;
                }

                // Do not take over a different Campfire installation the user happens to run.
                string? image = null;
                try
                {
                    image = process.MainModule?.FileName;
                }
                catch
                {
                    // Inspection can fail for protected processes; fall back to adopting it.
                }

                if (image is not null
                    && !string.Equals(Path.GetFullPath(image), expected, StringComparison.OrdinalIgnoreCase))
                {
                    process.Dispose();
                    continue;
                }

                return process;
            }
            catch
            {
                // Ignore processes that vanish between enumeration and inspection.
                process.Dispose();
            }
        }
        return null;
    }

    private void AttachProcess(Process process)
    {
        if (ReferenceEquals(_campfire, process))
            return;

        if (_campfire is { } previous)
        {
            previous.Exited -= OnCampfireExited;
            previous.Dispose();
        }

        _campfire = process;
        process.EnableRaisingEvents = true;
        process.Exited += OnCampfireExited;
    }

    /// <summary>Refresh every label and button from the current process state.</summary>
    private void RefreshUi()
    {
        var running = IsCampfireRunning();
        var installed = File.Exists(CampfireUpdater.ProgramPath);
        var installedVersion = CampfireUpdater.ReadInstalledVersion();
        var installedSize = installed ? ReadFileSize(CampfireUpdater.ProgramPath) : 0;

        if (!running)
        {
            if (!installed)
                _state = MultiplayerState.NotInstalled;
            else if (_state is MultiplayerState.NotInstalled or MultiplayerState.Running)
                _state = MultiplayerState.NotRunning;
        }

        var versionText = installed
            ? $"{(installedVersion is null ? Loc.T("版本未知") : DisplayVersion(installedVersion))} · {installedSize / 1024d / 1024d:0.0} MB"
            : Loc.T("未安装");

        VersionText.Text = versionText;
        PathText.Text = CampfireUpdater.ProgramPath;

        switch (_state)
        {
            case MultiplayerState.NotInstalled:
                HeadlineText.Text = Loc.T("还差一个组件");
                StatusText.Text = Loc.T("没有找到 Campfire。点「检查更新」，下载完成后会自动载入。");
                StatusBadgeText.Text = Loc.T("未安装");
                StatusIndicator.Background = ThemeResourceBrush("Brush.Danger");
                break;

            case MultiplayerState.NotRunning:
                HeadlineText.Text = Loc.T("准备就绪");
                StatusText.Text = Loc.T("点「打开 Campfire」开始，进入世界后把邀请码发给好友即可。");
                StatusBadgeText.Text = Loc.T("未运行");
                StatusIndicator.Background = ThemeResourceBrush("Brush.Warning");
                break;

            case MultiplayerState.Launching:
                HeadlineText.Text = Loc.T("正在打开…");
                StatusText.Text = Loc.T("Campfire 正在启动，稍候即可开始联机。");
                StatusBadgeText.Text = Loc.T("启动中");
                StatusIndicator.Background = ThemeResourceBrush("Brush.Accent");
                break;

            case MultiplayerState.Running:
                HeadlineText.Text = Loc.T("联机进行中");
                StatusText.Text = Loc.T("Campfire 在自己的窗口里运行，随时可以切回或关闭。");
                StatusBadgeText.Text = Loc.T("运行中");
                StatusIndicator.Background = ThemeResourceBrush("Brush.Success");
                break;
        }

        LaunchButton.Content = running ? Loc.T("切换到 Campfire") : Loc.T("打开 Campfire");
        LaunchButton.IsEnabled = installed && !_busy && _state != MultiplayerState.Launching;
        StopButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        StopButton.IsEnabled = !_busy;

        CheckButton.IsEnabled = !_busy;
        CheckButton.Visibility = _downloading ? Visibility.Collapsed : Visibility.Visible;
        CancelUpdateButton.Visibility = _downloading ? Visibility.Visible : Visibility.Collapsed;
        CancelUpdateButton.IsEnabled = _downloading;
        if (_latest is not null)
        {
            LatestHintText.Text = $"→ {Loc.T("最新版本")} v{_latest.Version}";
            var comparison = CompareVersions(installedVersion, ParseVersion(_latest.Version));
            var newer = comparison < 0;
            UpdateButton.Visibility = newer && !_downloading ? Visibility.Visible : Visibility.Collapsed;
            UpdateButton.Content = installedVersion is null
                ? string.Format(Loc.T("下载并安装 {0}"), _latest.Tag)
                : string.Format(Loc.T("更新到 {0}"), _latest.Tag);
            UpdateButton.IsEnabled = !_busy && newer;
        }
        else
        {
            LatestHintText.Text = "";
            UpdateButton.Visibility = Visibility.Collapsed;
        }

        if (!_busy)
        {
            if (_latest is null)
            {
                UpdateStatusText.Text = Loc.T("还没有检查更新。");
            }
            else
            {
                var comparison = CompareVersions(installedVersion, ParseVersion(_latest.Version));
                UpdateStatusText.Text = comparison < 0
                    ? string.Format(Loc.T("发现新版本 {0}，可以更新。"), _latest.Tag)
                    : comparison == 0
                        ? Loc.T("已是最新版本。")
                        : Loc.T("当前版本比发布页新。");
            }
            UpdateProgress.Visibility = Visibility.Collapsed;
        }

        if (_detail is { } detail && !_busy)
            StatusText.Text = detail;
    }

    /// <summary>Compare installed and published versions: negative when an update is available.</summary>
    private static int CompareVersions(Version? installed, Version? latest)
    {
        if (installed is null)
            return -1;
        if (latest is null)
            return -1;
        return Normalize(installed).CompareTo(Normalize(latest));
    }

    private static Version Normalize(Version version)
        => new(version.Major, version.Minor, version.Build < 0 ? 0 : version.Build);

    private static string DisplayVersion(Version version)
        => $"v{version.Major}.{version.Minor}.{(version.Build < 0 ? 0 : version.Build)}";

    private static long ReadFileSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static Version? ParseVersion(string text)
        => Version.TryParse(text.TrimStart('v', 'V'), out var version) ? version : null;

    private static Brush? ThemeResourceBrush(string key)
    {
        var resources = Application.Current.Resources;
        var dictKey = ThemeManager.IsDark ? "Default" : "Light";
        if (resources.ThemeDictionaries.TryGetValue(dictKey, out var dict)
            && dict is ResourceDictionary rd
            && rd.TryGetValue(key, out var value)
            && value is Brush brush)
            return brush;
        return null;
    }

    /// <summary>Show a short transient message in the main status line.</summary>
    private void SetDetail(string? text)
    {
        _detailTimer?.Stop();
        _detailTimer = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            _detail = null;
            RefreshUi();
            return;
        }

        _detail = text;
        StatusText.Text = text;

        _detailTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _detailTimer.Tick += (_, _) =>
        {
            _detailTimer?.Stop();
            _detailTimer = null;
            _detail = null;
            RefreshUi();
        };
        _detailTimer.Start();
    }

    private DispatcherTimer? _detailTimer;
    private string? _detail;

    private void SetState(MultiplayerState state)
    {
        _state = state;
        RefreshUi();
    }

    private void SetBusy(bool busy, bool indeterminate, string? status)
    {
        _busy = busy;
        UpdateProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        UpdateProgress.IsIndeterminate = indeterminate;
        if (busy)
        {
            if (!indeterminate)
                UpdateProgress.Value = 0;
            UpdateStatusText.Text = status ?? "";
        }
        RefreshUi();
    }

    // ---------------------------------------------------------- Opening Campfire

    private async void OnLaunchCampfire(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        if (IsCampfireRunning())
        {
            ActivateCampfireWindow();
            return;
        }

        if (!File.Exists(CampfireUpdater.ProgramPath))
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("找不到 Campfire"),
                Loc.T("可点「检查更新」从官方发布页下载并安装。"), MsgMark.Warning);
            return;
        }

        SetState(MultiplayerState.Launching);
        SetDetail(null);

        try
        {
            var process = Process.Start(new ProcessStartInfo(CampfireUpdater.ProgramPath)
            {
                UseShellExecute = true,
                WorkingDirectory = CampfireUpdater.ProgramDirectory,
            });

            if (process is null)
            {
                await AppServices.Dialogs.AlertAsync(Loc.T("无法打开 Campfire"),
                    Loc.T("Campfire 进程未能启动。"), MsgMark.Error);
                SetState(MultiplayerState.NotRunning);
                return;
            }

            AttachProcess(process);

            await WaitForStartupAsync(process, TimeSpan.FromSeconds(10));
            if (process.HasExited)
            {
                await AppServices.Dialogs.AlertAsync(Loc.T("Campfire 启动失败"),
                    string.Format(Loc.T("Campfire 启动后立即退出（退出码 {0}）。"), process.ExitCode), MsgMark.Error);
                SetState(MultiplayerState.NotRunning);
                return;
            }

            SetState(MultiplayerState.Running);
            AppLog.Info("Multiplayer", "已打开 Campfire");
        }
        catch (Exception ex)
        {
            AppLog.Error("Multiplayer", ex);
            await AppServices.Dialogs.AlertAsync(Loc.T("打开 Campfire 失败"), ex.Message, MsgMark.Error);
            SetState(File.Exists(CampfireUpdater.ProgramPath)
                ? MultiplayerState.NotRunning
                : MultiplayerState.NotInstalled);
        }
    }

    private void ActivateCampfireWindow()
    {
        try
        {
            var process = _campfire ?? FindRunningCampfire();
            if (process is null)
                return;

            process.Refresh();
            var hwnd = process.MainWindowHandle;
            if (hwnd == IntPtr.Zero)
                return;

            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Multiplayer", "切换到 Campfire 窗口失败:" + ex.Message);
        }
    }

    private static async Task WaitForStartupAsync(Process process, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (process.HasExited)
                    return;
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                    return;
            }
            catch
            {
                return;
            }

            await Task.Delay(200);
        }
    }

    private async void OnStopCampfire(object sender, RoutedEventArgs e)
    {
        if (_busy || !IsCampfireRunning())
            return;

        var confirmed = await AppServices.Dialogs.ConfirmAsync(
            Loc.T("关闭 Campfire"),
            Loc.T("确定要关闭 Campfire 吗？正在进行的联机会中断。"),
            MsgMark.Question,
            Loc.T("关闭"),
            Loc.T("取消"));
        if (!confirmed)
            return;

        await CloseCampfireAsync();
        SetState(File.Exists(CampfireUpdater.ProgramPath)
            ? MultiplayerState.NotRunning
            : MultiplayerState.NotInstalled);
    }

    /// <summary>Close the running program and wait briefly for its file handle to be released.</summary>
    private async Task<bool> CloseCampfireAsync()
    {
        if (_campfire is not { } process)
            return true;

        try
        {
            if (!process.HasExited)
            {
                // Ask the program to close normally first; force it only if it does not respond.
                if (!process.CloseMainWindow())
                    process.Kill();

                using (var grace = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
                {
                    try
                    {
                        await process.WaitForExitAsync(grace.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        if (!process.HasExited)
                            process.Kill();
                    }
                }
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            AppLog.Warn("Multiplayer", "等待 Campfire 退出超时");
        }
        catch (Exception ex)
        {
            AppLog.Warn("Multiplayer", "关闭 Campfire 失败:" + ex.Message);
        }

        return !IsCampfireRunning();
    }

    private void OnCampfireExited(object? sender, EventArgs e)
    {
        _ui.TryEnqueue(() =>
        {
            SetState(File.Exists(CampfireUpdater.ProgramPath)
                ? MultiplayerState.NotRunning
                : MultiplayerState.NotInstalled);
        });
    }

    // ---------------------------------------------------------- Updates

    private async void OnCheckUpdates(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        SetBusy(true, true, Loc.T("正在从官方发布页获取最新版本…"));
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _latest = await CampfireUpdater.GetLatestAsync(timeout.Token);
            AppLog.Info("Multiplayer", $"检测到 Campfire 最新版本 {_latest.Tag}");
            SetBusy(false, false, null);
            RefreshUi();
        }
        catch (Exception ex)
        {
            AppLog.Warn("Multiplayer", "检查 Campfire 更新失败:" + ex.Message);
            SetBusy(false, false, null);
            RefreshUi();
            await AppServices.Dialogs.AlertAsync(Loc.T("检查更新失败"),
                DescribeUpdateError(ex), MsgMark.Error);
        }
    }

    private async void OnUpdateCampfire(object sender, RoutedEventArgs e)
    {
        if (_busy || _latest is not { } release)
            return;

        if (IsCampfireRunning())
        {
            var confirmed = await AppServices.Dialogs.ConfirmAsync(
                Loc.T("需要先关闭 Campfire"),
                Loc.T("更新会关闭正在运行的 Campfire，是否继续？"),
                MsgMark.Question,
                Loc.T("关闭并更新"),
                Loc.T("取消"));
            if (!confirmed)
                return;

            await CloseCampfireAsync();
            if (IsCampfireRunning())
            {
                await AppServices.Dialogs.AlertAsync(Loc.T("无法更新"),
                    Loc.T("Campfire 仍在运行，请先手动关闭后再试。"), MsgMark.Warning);
                return;
            }
            SetState(MultiplayerState.NotRunning);
        }

        _downloading = true;
        _updateCts?.Dispose();
        _updateCts = new CancellationTokenSource();
        var cts = _updateCts;

        SetBusy(true, false, string.Format(Loc.T("正在下载 {0}…"), release.Tag));
        UpdateProgress.Value = 0;

        string? temporaryFile = null;
        try
        {
            var progress = new Progress<DownloadProgressInfo>(update =>
            {
                var total = update.Total > 0 ? update.Total : release.Size;
                if (total > 0)
                {
                    var percent = Math.Clamp(update.Received * 100d / total, 0, 100);
                    UpdateProgress.Value = percent;
                    UpdateStatusText.Text = string.Format(Loc.T("正在下载 {0}… {1}% · {2}"),
                        release.Tag, (int)percent, FormatSpeed(update.BytesPerSecond));
                }
                else
                {
                    UpdateStatusText.Text = string.Format(Loc.T("正在下载 {0}… {1}"),
                        release.Tag, FormatSpeed(update.BytesPerSecond));
                }
            });

            temporaryFile = await CampfireUpdater.DownloadAsync(release, progress, cts.Token);
            cts.Token.ThrowIfCancellationRequested();
            UpdateStatusText.Text = Loc.T("正在校验并替换文件…");

            var downloaded = temporaryFile;
            await Task.Run(() => CampfireUpdater.InstallDownloaded(downloaded));
            temporaryFile = null;

            _latest = release;
            AppLog.Info("Multiplayer", $"Campfire 已更新到 {release.Tag}");
            AppServices.Notify.Publish(NotifyKind.Success, string.Format(Loc.T("Campfire 已更新到 {0}"), release.Tag));

            _downloading = false;
            SetBusy(false, false, null);
            RefreshUi();
            await AppServices.Dialogs.AlertAsync(Loc.T("更新完成"),
                string.Format(Loc.T("Campfire 已更新到 {0}，可点「打开 Campfire」启动。"), release.Tag),
                MsgMark.Success);
        }
        catch (OperationCanceledException)
        {
            AppLog.Info("Multiplayer", "已取消 Campfire 更新");
            CampfireUpdater.TryDelete(temporaryFile ?? "");
            _downloading = false;
            SetBusy(false, false, null);
            RefreshUi();
            UpdateStatusText.Text = Loc.T("已取消更新。");
        }
        catch (Exception ex)
        {
            AppLog.Error("Multiplayer", ex);
            CampfireUpdater.TryDelete(temporaryFile ?? "");
            _downloading = false;
            SetBusy(false, false, null);
            RefreshUi();
            await AppServices.Dialogs.AlertAsync(Loc.T("更新失败"), DescribeUpdateError(ex), MsgMark.Error);
        }
        finally
        {
            _downloading = false;
            if (ReferenceEquals(_updateCts, cts))
                _updateCts = null;
            cts.Dispose();
        }
    }

    private void OnCancelUpdate(object sender, RoutedEventArgs e)
    {
        _updateCts?.Cancel();
        CancelUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = Loc.T("正在取消…");
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond >= 1024d * 1024d)
            return $"{bytesPerSecond / 1024d / 1024d:0.0} MB/s";
        if (bytesPerSecond >= 1024d)
            return $"{bytesPerSecond / 1024d:0} KB/s";
        return $"{bytesPerSecond:0} B/s";
    }

    private static string DescribeUpdateError(Exception ex) => ex switch
    {
        HttpRequestException => Loc.T("无法连接 Campfire 下载服务器，请检查网络后重试。"),
        TaskCanceledException => Loc.T("请求超时，请检查网络后重试。"),
        UnauthorizedAccessException => Loc.T("没有写入权限，无法更新 Campfire。请把启动器放到可写目录，或手动下载后替换。"),
        InvalidDataException => ex.Message,
        IOException => Loc.T("写入文件失败：") + ex.Message,
        _ => ex.Message,
    };

    // ---------------------------------------------------------- Utilities

    private void OnCopyPath(object sender, RoutedEventArgs e)
    {
        try
        {
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            package.SetText(CampfireUpdater.ProgramPath);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            SetDetail(Loc.T("路径已复制"));
        }
        catch
        {
            SetDetail(Loc.T("复制失败"));
        }
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            var directory = CampfireUpdater.ProgramDirectory;
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
            Loc.T("1. 点「打开 Campfire」，它会以独立窗口运行。\n2. 在 Campfire 里进入一个世界，它会自动对局域网开放并生成邀请码。\n3. 把邀请码发给好友，好友在 Campfire 里粘贴即可加入；也可以在「联机大厅」里选择房间。\n4. 联机时游戏数据优先在玩家之间直接传输，只有建立连接所需的信令经过 Campfire 的服务器，启动器不参与任何联机通信。\n5. Campfire 会在新手引导结束后询问是否发送匿名使用统计，之后可在它的设置里修改。"),
            MsgMark.Info);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}

/// <summary>联机页面的状态机。</summary>
internal enum MultiplayerState
{
    /// <summary>没有可用的 Campfire 文件。</summary>
    NotInstalled,
    /// <summary>文件存在但未运行。</summary>
    NotRunning,
    /// <summary>正在启动。</summary>
    Launching,
    /// <summary>独立窗口运行中。</summary>
    Running,
}
