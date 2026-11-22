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
using Windows.Storage.Pickers;
using SL.Core.Instances;
using SL.Core.Launch;
using SL.Core.Boot;
using SL.Core.Logging;
using SL.App.Localization;
using SL.App.Dialogs;
using SL.App.Instances;
using SL.App.Infrastructure;
using SL.App.Notify;

namespace SL.App.Pages;

/// <summary>Mod row (bound to the mod list). Enabled is observable so a toggle can update it in place.</summary>
public sealed class ModRow : System.ComponentModel.INotifyPropertyChanged
{
    private bool _enabled;

    public ModRow(ModInfo info)
    {
        FileName = info.FileName;
        Name = info.BaseName;
        _enabled = info.Enabled;
        SizeBytes = info.SizeBytes;
        ModifiedAt = info.ModifiedAt;
    }

    public string FileName { get; }
    public string Name { get; }
    public long SizeBytes { get; }
    public DateTimeOffset ModifiedAt { get; }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;
            _enabled = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Enabled)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Detail)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Glyph)));
        }
    }

    public string Detail => $"{SizeBytes / 1024.0:F0} KB · {ModifiedAt:yyyy-MM-dd HH:mm}"
                            + (Enabled ? "" : Loc.T(" · 已停用"));

    public string Glyph => Enabled ? "\uE7B8" : "\uE7BA";

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>Mod management page: enable / disable (via the .disabled suffix), delete, and import from local files.</summary>
public sealed partial class InstanceModsPage : Page
{
    private readonly ObservableCollection<InstancePick> _picks = [];
    private readonly ObservableCollection<ModRow> _rows = [];
    private bool _syncing;
    private bool _importing;

    public InstanceModsPage()
    {
        InitializeComponent();
        InstanceBox.ItemsSource = _picks;
        ModList.ItemsSource = _rows;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Reload();
    }

    private void Reload()
    {
        _syncing = true;
        var all = InstanceContext.Refresh();
        _picks.Clear();
        foreach (var info in all)
            _picks.Add(new InstancePick(info));
        InstanceBox.SelectedItem = _picks.FirstOrDefault(p => p.Id == InstanceContext.CurrentId);
        _syncing = false;

        _rows.Clear();
        if (InstanceContext.CurrentId.Length == 0)
        {
            StatusText.Text = Loc.T("尚未安装实例 · 请先安装一个版本");
            return;
        }

        foreach (var mod in InstanceRegistry.ListMods(InstanceContext.CurrentId))
            _rows.Add(new ModRow(mod));

        RefreshSummary();
    }

    /// <summary>Refresh the status line from the current rows without rebuilding the list.</summary>
    private void RefreshSummary()
    {
        if (InstanceContext.CurrentId.Length == 0)
        {
            StatusText.Text = Loc.T("尚未安装实例 · 请先安装一个版本");
            return;
        }

        var enabled = _rows.Count(r => r.Enabled);
        StatusText.Text = _rows.Count == 0
            ? string.Format(Loc.T("实例 {0} 的 mods 目录为空 · 可从「资源 → 模组仓库」安装"), InstanceContext.CurrentId)
            : string.Format(Loc.T("实例 {0} · 共 {1} 个模组,启用 {2} 个"), InstanceContext.CurrentId, _rows.Count, enabled);
    }

    private void OnInstanceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || InstanceBox.SelectedItem is not InstancePick pick)
            return;
        InstanceContext.Select(pick.Id);
        Reload();
    }

    private async void OnToggleMod(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle || toggle.DataContext is not ModRow row)
            return;
        if (InstanceContext.CurrentId.Length == 0)
            return;

        try
        {
            var enabled = InstanceRegistry.ToggleMod(InstanceContext.CurrentId, row.FileName);

            // Update the row in place. Rebuilding the list from inside the ToggleSwitch's own
            // Toggled event destroys the control mid-operation and crashes the XAML measure pass.
            row.Enabled = enabled;
            AppServices.Notify.Publish(NotifyKind.Info, (enabled ? Loc.T("已启用") : Loc.T("已停用")) + ":" + row.Name);
            RefreshSummary();
        }
        catch (Exception ex)
        {
            AppLog.Error("InstanceMods", ex);
            // Put the switch back where it was, so the UI still matches the files on disk.
            toggle.IsOn = row.Enabled;
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("切换失败:") + ex.Message);
        }

        await Task.CompletedTask;
    }

    private async void OnDeleteMod(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not ModRow row)
            return;
        if (InstanceContext.CurrentId.Length == 0)
            return;

        if (!await AppServices.Dialogs.ConfirmAsync(Loc.T("删除模组"),
                string.Format(Loc.T("将删除「{0}」,该操作不可撤销。"), row.Name), MsgMark.Warning).ConfigureAwait(true))
            return;

        try
        {
            InstanceRegistry.DeleteMod(InstanceContext.CurrentId, row.FileName);
            AppServices.Notify.Publish(NotifyKind.Info, Loc.T("已删除:") + row.Name);
        }
        catch (Exception ex)
        {
            AppLog.Error("InstanceMods", ex);
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("删除失败:") + ex.Message);
        }
        Reload();
    }

    private async void OnImport(object sender, RoutedEventArgs e)
    {
        if (InstanceContext.CurrentId.Length == 0)
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("尚未安装实例"), Loc.T("请先安装一个实例再导入模组。"),
                MsgMark.Info).ConfigureAwait(true);
            return;
        }

        try
        {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.Window));
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.FileTypeFilter.Add(".jar");

            var file = await picker.PickSingleFileAsync();
            if (file is null)
                return;

            var name = InstanceRegistry.ImportMod(InstanceContext.CurrentId, file.Path);
            AppServices.Notify.Publish(NotifyKind.Success, Loc.T("已导入:") + name);
        }
        catch (Exception ex)
        {
            AppLog.Error("InstanceMods", ex);
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("导入失败:") + ex.Message);
        }
        Reload();
    }

    /// <summary>
    /// Zip modpack import: pick a .zip / .mrpack, read its manifest, show what it needs, then place every
    /// file into the current instance (downloading what the pack links to, extracting what it carries).
    /// </summary>
    private async void OnImportPack(object sender, RoutedEventArgs e)
    {
        if (InstanceContext.CurrentId.Length == 0)
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("尚未安装实例"),
                Loc.T("请先安装一个实例(建议先装好整合包要求的版本与加载器),再导入整合包。"),
                MsgMark.Info).ConfigureAwait(true);
            return;
        }

        if (_importing)
            return;

        string zipPath;
        try
        {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.Window));
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.FileTypeFilter.Add(".zip");
            picker.FileTypeFilter.Add(".mrpack");
            picker.FileTypeFilter.Add(".jar");

            var file = await picker.PickSingleFileAsync();
            if (file is null)
                return;
            zipPath = file.Path;
        }
        catch (Exception ex)
        {
            AppLog.Error("Modpack", ex);
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("打开文件失败:") + ex.Message);
            return;
        }

        ModpackManifest manifest;
        try
        {
            manifest = await Task.Run(() => ModpackImporter.Inspect(zipPath)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppLog.Error("Modpack", ex);
            await AppServices.Dialogs.AlertAsync(Loc.T("无法识别整合包"),
                Loc.T("这个压缩包不是可识别的整合包格式:") + ex.Message, MsgMark.Error).ConfigureAwait(true);
            return;
        }

        if (manifest.Files.Count == 0)
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("整合包里没有可导入的文件"),
                string.Join("\n", manifest.Notes), MsgMark.Warning).ConfigureAwait(true);
            return;
        }

        // Warn about a loader/version mismatch before touching the disk.
        var warnings = new List<string>();
        var current = InstanceContext.CurrentId;
        var matchesLoader = !manifest.NeedsLoader
                            || current.EndsWith("-" + manifest.Loader.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
        var matchesVersion = manifest.GameVersion.Length == 0
                             || current.StartsWith(manifest.GameVersion, StringComparison.OrdinalIgnoreCase);
        var mismatch = !matchesLoader || !matchesVersion;

        if (!matchesLoader)
            warnings.Add(string.Format(Loc.T("这个整合包需要 {0} 实例,当前实例是「{1}」,直接导入可能无法启动。"),
                manifest.Loader, current));
        if (!matchesVersion)
            warnings.Add(string.Format(Loc.T("整合包面向 Minecraft {0},当前实例是「{1}」,版本不匹配可能崩溃。"),
                manifest.GameVersion, current));
        warnings.AddRange(manifest.Notes);

        var summary =
            string.Format(Loc.T("名称:{0}"), manifest.Name) + "\n" +
            (manifest.Version.Length > 0 ? string.Format(Loc.T("版本:{0}"), manifest.Version) + "\n" : "") +
            (manifest.GameVersion.Length > 0 ? string.Format(Loc.T("Minecraft:{0}"), manifest.GameVersion) + "\n" : "") +
            (manifest.NeedsLoader
                ? string.Format(Loc.T("加载器:{0} {1}"), manifest.Loader, manifest.LoaderVersion) + "\n"
                : "") +
            string.Format(Loc.T("文件:{0} 个(其中模组 {1} 个)"), manifest.Files.Count, manifest.ModCount) + "\n" +
            string.Format(Loc.T("目标实例:{0}"), current);

        if (warnings.Count > 0)
            summary += "\n\n" + string.Join("\n", warnings.Select(w => "⚠ " + w));

        // A pack that wants a different loader/version can create its own instance in one step,
        // which is what "one-click modpack" has to mean: no manual detour through the install wizard.
        var targetInstance = current;
        var createInstance = false;
        if (mismatch && manifest.NeedsLoader && manifest.GameVersion.Length > 0)
        {
            var wanted = $"{manifest.GameVersion}-{manifest.Loader.ToLowerInvariant()}";
            var choice = await AppServices.Dialogs.ChooseAsync(Loc.T("导入整合包"), summary, [
                string.Format(Loc.T("自动创建 {0} 实例并导入(推荐)"), wanted),
                string.Format(Loc.T("仍然导入到当前实例 {0}"), current),
                Loc.T("取消"),
            ]).ConfigureAwait(true);

            if (choice is null or 2)
                return;
            if (choice == 0)
            {
                targetInstance = wanted;
                createInstance = true;
            }
        }
        else if (!await AppServices.Dialogs.ConfirmAsync(Loc.T("导入整合包"), summary, MsgMark.Info).ConfigureAwait(true))
        {
            return;
        }

        _importing = true;
        ImportButton.IsEnabled = false;
        var started = DateTimeOffset.Now;
        try
        {
            if (createInstance)
            {
                StatusText.Text = string.Format(Loc.T("正在准备 {0} 实例…"), targetInstance);
                await CreatePackInstanceAsync(manifest, targetInstance).ConfigureAwait(true);
            }

            StatusText.Text = Loc.T("正在导入整合包…");
            var progress = new Progress<string>(text => StatusText.Text = text);
            var result = await Task.Run(() => ModpackImporter.ImportAsync(zipPath, targetInstance,
                text => ((IProgress<string>)progress).Report(text), CancellationToken.None)).ConfigureAwait(true);

            var seconds = (int)(DateTimeOffset.Now - started).TotalSeconds;
            AppLog.Info("Modpack", $"导入完成 {result.Imported} 个文件 / 下载 {result.Downloaded} 个 / 跳过 {result.Skipped} 个");

            var message = string.Format(Loc.T("整合包导入完成({0}s):解压 {1} 个、下载 {2} 个、跳过已存在 {3} 个"),
                seconds, result.Imported, result.Downloaded, result.Skipped);
            if (result.Failed > 0)
            {
                var detail = string.Join("\n", result.Failures.Take(8));
                await AppServices.Dialogs.AlertAsync(Loc.T("部分文件未导入"),
                    message + "\n\n" + string.Format(Loc.T("失败 {0} 个:"), result.Failed) + "\n" + detail,
                    MsgMark.Warning).ConfigureAwait(true);
            }
            else
            {
                AppServices.Notify.Publish(NotifyKind.Success, message);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Modpack", ex);
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("整合包导入失败:") + ex.Message);
        }
        finally
        {
            _importing = false;
            ImportButton.IsEnabled = true;
            Reload();
        }
    }

    /// <summary>
    /// Build the loader instance an imported pack asks for: fetch whatever vanilla files are missing, then
    /// merge the loader profile and queue the loader libraries. No prior manual vanilla install required.
    /// </summary>
    private async Task CreatePackInstanceAsync(ModpackManifest manifest, string instanceId)
    {
        var progress = new Progress<string>(text => StatusText.Text = text);
        void Step(string text) => ((IProgress<string>)progress).Report(text);

        await VanillaInstaller.EnsureAsync(manifest.GameVersion, null, CancellationToken.None, Step)
            .ConfigureAwait(true);

        var vanillaJson = System.IO.Path.Combine(Runtime.InstancesDir, manifest.GameVersion, "meta",
            manifest.GameVersion + ".json");
        var vanillaJar = System.IO.Path.Combine(Runtime.InstancesDir, manifest.GameVersion, "versions",
            manifest.GameVersion + ".jar");

        var loaderVersion = manifest.LoaderVersion.Length > 0 ? manifest.LoaderVersion : null;
        var plan = await LoaderInstall.BuildPlanAsync(manifest.Loader, manifest.GameVersion, loaderVersion,
                vanillaJson, vanillaJar, CancellationToken.None, Step)
            .ConfigureAwait(true);

        var queued = Download.InstallWizard.SubmitLoaderFiles(plan.Files);
        AppLog.Info("Modpack", $"为整合包创建实例 {plan.InstanceId},提交 {queued} 个库文件");
        AppServices.Notify.Publish(NotifyKind.Info,
            string.Format(Loc.T("已创建实例 {0},正在后台下载 {1} 个库文件"), plan.InstanceId, queued));

        if (!plan.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase))
            AppLog.Warn("Modpack", $"加载器生成的实例名 {plan.InstanceId} 与预期 {instanceId} 不同,以实际为准");

        InstanceContext.Refresh();
        InstanceContext.Select(plan.InstanceId);
    }

    // ---------------------------------------------------------- 模组依赖检查

    /// <summary>静态检查当前实例的模组依赖与重复安装。</summary>
    private async void OnCheckDependencies(object sender, RoutedEventArgs e)
    {
        var instanceId = SL.App.Instances.InstanceContext.CurrentId;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            await AppServices.Dialogs.AlertAsync("尚未选择实例",
                "请先在「实例」里选中一个实例。", MsgMark.Info).ConfigureAwait(true);
            return;
        }

        try
        {
            var modsDir = SL.Core.Instances.InstanceRegistry.ModsDirOf(instanceId);
            var issues = await System.Threading.Tasks.Task.Run(
                () => SL.Core.Instances.ModDependencyChecker.Check(modsDir)).ConfigureAwait(true);

            var summary = SL.Core.Instances.ModDependencyChecker.Summarize(issues);

            if (issues.Count == 0)
            {
                AppServices.Notify.Publish(NotifyKind.Success, summary);
                await AppServices.Dialogs.AlertAsync("依赖检查", summary, MsgMark.Success).ConfigureAwait(true);
                return;
            }

            var lines = issues.Take(12)
                .Select(i => "· " + i.Message)
                .ToList();

            var body = summary + "\n\n" + string.Join("\n", lines);
            if (issues.Count > 12)
                body += "\n\u2026 共 " + issues.Count + " 条";

            var hasError = issues.Any(i => i.Level == SL.Core.Instances.ModIssueLevel.Error);
            AppServices.Notify.Publish(hasError ? NotifyKind.Warning : NotifyKind.Info, summary);

            await AppServices.Dialogs.AlertAsync("依赖检查", body,
                hasError ? MsgMark.Warning : MsgMark.Info).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppLog.Error("InstanceMods", ex);
            await AppServices.Dialogs.AlertAsync("依赖检查失败", ex.Message, MsgMark.Error)
                .ConfigureAwait(true);
        }
    }

    private void OnOpenDir(object sender, RoutedEventArgs e)
    {
        if (InstanceContext.CurrentId.Length == 0)
            return;
        try
        {
            var dir = InstanceRegistry.ModsDirOf(InstanceContext.CurrentId);
            System.IO.Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Warn("InstanceMods", "打开目录失败:" + ex.Message);
        }
    }
}