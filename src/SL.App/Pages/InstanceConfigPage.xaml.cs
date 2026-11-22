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
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SL.Core.Instances;
using SL.Core.Logging;
using SL.App.Localization;
using SL.App.Dialogs;
using SL.App.Instances;
using SL.App.Infrastructure;
using SL.App.Notify;

namespace SL.App.Pages;

/// <summary>Instance settings page: launch arguments (applied globally) and instance directory maintenance.</summary>
public sealed partial class InstanceConfigPage : Page
{
    private readonly ObservableCollection<InstancePick> _picks = [];
    private bool _syncing;
    private bool _loading;

    public InstanceConfigPage()
    {
        InitializeComponent();
        InstanceBox.ItemsSource = _picks;
    
        RefreshJavaSummary();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _syncing = true;
        var all = InstanceContext.Refresh();
        _picks.Clear();
        foreach (var info in all)
            _picks.Add(new InstancePick(info));
        InstanceBox.SelectedItem = _picks.FirstOrDefault(p => p.Id == InstanceContext.CurrentId);
        _syncing = false;
        LoadValues();
    }

    private void LoadValues()
    {
        _loading = true;
        var cfg = AppServices.Settings.Current;
        MemoryBox.Value = cfg.MemoryMb;
        WidthBox.Value = cfg.GameWidth;
        HeightBox.Value = cfg.GameHeight;
        JvmBox.Text = cfg.JvmExtraArgs;
        _loading = false;

        if (InstanceContext.CurrentId.Length == 0)
        {
            StatusText.Text = Loc.T("尚未安装实例 · 请先安装一个版本");
            PathText.Text = "—";
            return;
        }

        StatusText.Text = string.Format(Loc.T("当前实例 {0}"), InstanceContext.CurrentId);
        PathText.Text = InstanceRegistry.InstanceDirOf(InstanceContext.CurrentId);
    }

    private void OnInstanceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || InstanceBox.SelectedItem is not InstancePick pick)
            return;
        InstanceContext.Select(pick.Id);
        LoadValues();
    }

    private void OnMemoryChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(args.NewValue))
            return;
        var value = (int)args.NewValue;
        AppServices.Settings.Update(c => c.MemoryMb = value);
    }

    private void OnWindowChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(args.NewValue))
            return;
        var width = double.IsNaN(WidthBox.Value) ? 854 : (int)WidthBox.Value;
        var height = double.IsNaN(HeightBox.Value) ? 480 : (int)HeightBox.Value;
        AppServices.Settings.Update(c =>
        {
            c.GameWidth = width;
            c.GameHeight = height;
        });
    }

    private void OnJvmChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        var text = JvmBox.Text.Trim();
        AppServices.Settings.Update(c => c.JvmExtraArgs = text);
    }

    // ---------------------------------------------------------- 克隆 / 备份 / 恢复

    // ---------------------------------------------------------- Java 运行时

    private void RefreshJavaSummary()
    {
        try
        {
            var all = SL.Core.Launch.JavaRuntime.Detect(forceRefresh: true);
            if (all.Count == 0)
            {
                JavaSummaryText.Text = "未检测到 Java。点「下载推荐版本」自动获取。";
                return;
            }

            var lines = all.Take(4)
                .Select(j => "· Java " + j.MajorVersion + "（" + j.Version + "）" + (j.IsJdk ? " JDK" : " JRE"))
                .ToList();

            JavaSummaryText.Text = "检测到 " + all.Count + " 个运行时：\n" + string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            Core.Logging.AppLog.Warn("InstanceCfg", "检测 Java 失败:" + ex.Message);
            JavaSummaryText.Text = "检测 Java 失败：" + ex.Message;
        }
    }

    private void OnRescanJava(object sender, RoutedEventArgs e)
    {
        RefreshJavaSummary();
        AppServices.Notify.Publish(SL.App.Notify.NotifyKind.Info, "已重新检测 Java");
    }

    private async void OnDownloadJava(object sender, RoutedEventArgs e)
    {
        var id = CurrentInstanceId();
        var major = SL.Core.Launch.JavaRuntime.RequiredMajorFor(id ?? "1.21.4");

        try
        {
            AppServices.Notify.Publish(SL.App.Notify.NotifyKind.Info,
                "正在准备 Java " + major + "…");

            var exe = await SL.Core.Launch.JavaRuntime.EnsureAsync(major, null, default, text =>
            {
                Core.Logging.AppLog.Info("InstanceCfg", "Java: " + text);
            }).ConfigureAwait(true);

            if (exe is null)
            {
                await AppServices.Dialogs.AlertAsync("Java 获取失败",
                    "自动下载未成功，可手动安装 Java " + major + " 后重试。",
                    MsgMark.Warning).ConfigureAwait(true);
                return;
            }

            AppServices.Notify.Publish(SL.App.Notify.NotifyKind.Success, "Java " + major + " 已就绪");
            RefreshJavaSummary();
        }
        catch (Exception ex)
        {
            Core.Logging.AppLog.Error("InstanceCfg", ex);
            await AppServices.Dialogs.AlertAsync("Java 获取失败", ex.Message, MsgMark.Error)
                .ConfigureAwait(true);
        }
    }

    private string? CurrentInstanceId()
    {
        var id = SL.App.Instances.InstanceContext.CurrentId;
        if (string.IsNullOrWhiteSpace(id))
            id = SL.Core.Instances.InstanceRegistry.Latest()?.InstanceId;
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    /// <summary>克隆当前实例为一份独立副本。</summary>
    private async void OnCloneInstance(object sender, RoutedEventArgs e)
    {
        var id = CurrentInstanceId();
        if (id is null)
            return;

        try
        {
            var newId = await System.Threading.Tasks.Task.Run(
                () => SL.Core.Instances.InstanceRegistry.DuplicateInstance(id)).ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(newId))
            {
                await AppServices.Dialogs.AlertAsync("克隆失败",
                    "未能创建副本,请确认磁盘空间是否充足。", MsgMark.Warning);
                return;
            }

            AppServices.Notify.Publish(SL.App.Notify.NotifyKind.Success, "已创建副本:" + newId);
            SL.App.Instances.InstanceContext.Refresh();
        }
        catch (Exception ex)
        {
            Core.Logging.AppLog.Error("InstanceCfg", ex);
            await AppServices.Dialogs.AlertAsync("克隆失败", ex.Message, MsgMark.Error);
        }
    }

    /// <summary>把当前实例导出为 zip 备份。</summary>
    private async void OnExportBackup(object sender, RoutedEventArgs e)
    {
        var id = CurrentInstanceId();
        if (id is null)
            return;

        var includeSaves = IncludeSavesBox.IsChecked == true;

        try
        {
            var zip = await System.Threading.Tasks.Task.Run(
                () => SL.Core.Instances.InstanceRegistry.ExportInstanceZip(id, true, true, includeSaves))
                .ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(zip) || !File.Exists(zip))
            {
                await AppServices.Dialogs.AlertAsync("导出失败",
                    "未能生成备份文件,请确认实例目录存在且可读。", MsgMark.Warning);
                return;
            }

            AppServices.Notify.Publish(SL.App.Notify.NotifyKind.Success,
                "备份已生成:" + Path.GetFileName(zip));

            // 直接打开所在目录,便于用户取走文件
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "explorer.exe", "/select,\"" + zip + "\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Core.Logging.AppLog.Error("InstanceCfg", ex);
            await AppServices.Dialogs.AlertAsync("导出失败", ex.Message, MsgMark.Error);
        }
    }

    /// <summary>从 zip 备份恢复当前实例。</summary>
    private async void OnImportBackup(object sender, RoutedEventArgs e)
    {
        var id = CurrentInstanceId();
        if (id is null)
            return;

        string? zip;
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.Window));
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            picker.FileTypeFilter.Add(".zip");
            var file = await picker.PickSingleFileAsync();
            zip = file?.Path;
        }
        catch (Exception ex)
        {
            Core.Logging.AppLog.Warn("InstanceCfg", "选择备份文件失败:" + ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(zip) || !File.Exists(zip))
            return;

        var ok = await AppServices.Dialogs.ConfirmAsync(
            "从备份恢复",
            "将把备份中的模组、配置与存档写入实例 " + id + ",同名文件会被覆盖。是否继续?",
            MsgMark.Question).ConfigureAwait(true);

        if (!ok)
            return;

        try
        {
            var restored = await System.Threading.Tasks.Task.Run(
                () => SL.Core.Instances.InstanceRegistry.ImportInstanceZip(zip, id)).ConfigureAwait(true);

            AppServices.Notify.Publish(SL.App.Notify.NotifyKind.Success,
                string.IsNullOrWhiteSpace(restored)
                    ? "恢复完成"
                    : "已恢复到实例:" + restored);

            SL.App.Instances.InstanceContext.Refresh();
        }
        catch (Exception ex)
        {
            Core.Logging.AppLog.Error("InstanceCfg", ex);
            await AppServices.Dialogs.AlertAsync("恢复失败", ex.Message, MsgMark.Error);
        }
    }

    private void OnOpenDir(object sender, RoutedEventArgs e)
    {
        if (InstanceContext.CurrentId.Length == 0)
            return;
        try
        {
            var dir = InstanceRegistry.InstanceDirOf(InstanceContext.CurrentId);
            System.IO.Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Warn("InstanceConfig", "打开目录失败:" + ex.Message);
        }
    }

    private async void OnDeleteInstance(object sender, RoutedEventArgs e)
    {
        if (InstanceContext.CurrentId.Length == 0)
            return;
        var id = InstanceContext.CurrentId;

        if (!await AppServices.Dialogs.ConfirmAsync(Loc.T("删除实例"),
                string.Format(Loc.T("将删除实例「{0}」的整个目录(含模组、存档与日志),该操作不可撤销。\n"), id)
                + Loc.T("共享的库与资源不会被删除。"), MsgMark.Warning, okLabel: Loc.T("删除")).ConfigureAwait(true))
            return;

        try
        {
            var dir = InstanceRegistry.InstanceDirOf(id);
            if (System.IO.Directory.Exists(dir))
                System.IO.Directory.Delete(dir, recursive: true);
            AppLog.Info("InstanceConfig", $"已删除实例 {id}");
            AppServices.Notify.Publish(NotifyKind.Info, string.Format(Loc.T("已删除实例 {0}"), id));
            InstanceContext.Select("");
            InstanceContext.Refresh();
            LoadValues();
        }
        catch (Exception ex)
        {
            AppLog.Error("InstanceConfig", ex);
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("删除失败:") + ex.Message);
        }
    }
}