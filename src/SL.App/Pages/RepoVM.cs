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
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SL.Core.Instances;
using SL.Core.Launch;
using SL.Core.Logging;
using SL.Core.Net;
using SL.App.Download;
using SL.App.Dialogs;
using SL.App.Infrastructure;
using SL.App.Notify;
using SL.App.Localization;

namespace SL.App.Pages;

/// <summary>Modrinth project card row.</summary>
public sealed class RepoRow
{
    public RepoRow(Modrinth.ProjectHit hit, RepoVM owner)
    {
        Hit = hit;
        InstallCommand = new RelayCommand(async () => await owner.InstallAsync(this));
    }

    public Modrinth.ProjectHit Hit { get; }
    public RelayCommand InstallCommand { get; }

    public string InstallText => Loc.T("下载并安装");

    public string Title => Hit.Title;
    public string Description => Hit.Description;
    public string Author => Loc.T("作者") + " " + Hit.Author;
    public string Downloads => Loc.T("下载") + " " + Hit.DownloadsText;
    public string Categories => string.Join(" · ", Hit.Categories);
}

/// <summary>Repository view model: shared by mods / shaders / resource packs; search and install both use the real network.</summary>
public sealed class RepoVM : VmBase, IRefreshable
{
    private readonly DispatcherQueue _ui = DispatcherQueue.GetForCurrentThread();
    private ModrinthCategory _category = ModrinthCategory.Mods;

    /// <summary>Categories already fetched once, so each page loads its own results exactly once.</summary>
    private readonly HashSet<ModrinthCategory> _loadedCategories = [];

    /// <summary>How many popular entries to skip when browsing. Advanced by the "another set" button.</summary>
    private int _browseOffset;

    public RepoVM()
    {
        SearchCommand = new RelayCommand(async () => await SearchAsync());
        ClearCommand = new RelayCommand(async () =>
        {
            if (_keyword.Length == 0)
                return;
            Keyword = "";
            _browseOffset = 0;
            await SearchAsync();
        });
        ShuffleCommand = new RelayCommand(async () =>
        {
            _browseOffset += Modrinth.PageSize;
            await SearchAsync();
        });
    }

    public ObservableCollection<RepoRow> Rows { get; } = [];

    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand ShuffleCommand { get; }

    private string _keyword = "";

    /// <summary>The search text. Setting it only updates state — requests are sent on submit, never per keystroke.</summary>
    public string Keyword
    {
        get => _keyword;
        set
        {
            if (Set(ref _keyword, value))
                OnBrowseState();
        }
    }

    /// <summary>True when the box is empty (whitespace counts as empty): the page then shows popular picks.</summary>
    public bool IsBrowsing => string.IsNullOrWhiteSpace(_keyword);

    /// <summary>Heading above the result list.</summary>
    public string SectionTitle => IsBrowsing ? Loc.T("热门推荐") : Loc.T("搜索结果");

    /// <summary>Sub-heading under <see cref="SectionTitle"/>; empty while searching.</summary>
    public string SectionHint => IsBrowsing ? Loc.T("按下载量排序 · 点「换一批」看更多") : "";

    /// <summary>The "another set" button only makes sense while browsing.</summary>
    public Visibility ShuffleVisibility => IsBrowsing ? Visibility.Visible : Visibility.Collapsed;

    public string ShuffleText => Loc.T("换一批");
    public string SearchLabel => Loc.T("搜索");

    private void OnBrowseState()
    {
        On(nameof(IsBrowsing));
        On(nameof(SectionTitle));
        On(nameof(SectionHint));
        On(nameof(ShuffleVisibility));
    }

    /// <summary>Back to popular picks from scratch (used when the search box is emptied).</summary>
    public async Task ResetToBrowseAsync()
    {
        if (_keyword.Length != 0)
            Keyword = "";
        _browseOffset = 0;
        await SearchAsync();
    }

    private string _title = Loc.T("模组仓库");
    public string Title { get => _title; private set => Set(ref _title, value); }

    private string _status = "";
    public string Status { get => _status; private set => Set(ref _status, value); }

    private bool _busy;
    public bool Busy
    {
        get => _busy;
        private set
        {
            if (Set(ref _busy, value))
                On(nameof(BusyVisibility));
        }
    }

    public Visibility BusyVisibility => Busy ? Visibility.Visible : Visibility.Collapsed;

    public string TargetText { get; private set; } = "";

    private string _targetInstance = "";
    private string _gameVersion = "";
    private string _loader = "fabric";

    public string SearchHint => _category switch
    {
        ModrinthCategory.Mods => Loc.T("搜索模组(如 sodium、create)"),
        ModrinthCategory.Shaders => Loc.T("搜索光影(如 complementary、bsl)"),
        _ => Loc.T("搜索资源包(如 faithful、stay true)"),
    };

    public string SubDir => _category switch
    {
        ModrinthCategory.Mods => "mods",
        ModrinthCategory.Shaders => "shaderpacks",
        _ => "resourcepacks",
    };

    /// <summary>The route key determines the category: dl.pack / dl.shaders / dl.packs.</summary>
    public void Configure(string routeKey)
    {
        _category = routeKey switch
        {
            "dl.shaders" => ModrinthCategory.Shaders,
            "dl.packs" => ModrinthCategory.ResourcePacks,
            _ => ModrinthCategory.Mods,
        };
        Title = _category switch
        {
            ModrinthCategory.Mods => Loc.T("模组仓库"),
            ModrinthCategory.Shaders => Loc.T("光影仓库"),
            _ => Loc.T("资源包仓库"),
        };
        // A different category means a different result set: restart paging from the top.
        _browseOffset = 0;
        On(nameof(SearchHint));
        On(nameof(TargetText));
        OnBrowseState();
    }

    public void RefreshData()
    {
        var latest = InstanceRegistry.Latest();
        _targetInstance = latest?.InstanceId ?? "";
        (_gameVersion, _loader) = SplitInstance(_targetInstance);
        TargetText = latest is null
            ? Loc.T("尚未安装实例 · 下载后将保存到实例目录,请先安装一个版本")
            : string.Format(Loc.T("安装目标:实例 {0}({1} · {2})→ {3}"), latest.InstanceId, _gameVersion, _loader, SubDir);
        On(nameof(TargetText));

        // One load per category: switching mods → shaders → resource packs must fetch that
        // category rather than keep showing whatever the previous page loaded.
        if (!_loadedCategories.Contains(_category))
        {
            _loadedCategories.Add(_category);
            _browseOffset = 0;
            _ = SearchAsync();
        }
    }

    /// <summary>Instance id → (game version, loader). Loader instances are named {version}-{loader}.</summary>
    private static (string Version, string Loader) SplitInstance(string instanceId)
    {
        if (instanceId.Length == 0)
            return ("1.21.4", "fabric");
        foreach (var loader in new[] { "neoforge", "forge", "quilt", "fabric" })
        {
            var suffix = "-" + loader;
            if (instanceId.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return (instanceId[..^suffix.Length], loader);
        }
        // Vanilla instances have no loader: default to Fabric when looking for mods (otherwise no installable content is found)
        return (instanceId, "fabric");
    }

    private async Task SearchAsync()
    {
        if (Busy)
            return;
        Busy = true;

        // Empty box → popular picks ordered by downloads; typed text → best match.
        var browsing = IsBrowsing;
        var sort = browsing ? ModrinthSort.Downloads : ModrinthSort.Relevance;
        var query = browsing ? "" : _keyword.Trim();

        Status = browsing ? Loc.T("正在加载热门内容…") : Loc.T("正在检索 Modrinth…");
        try
        {
            var gameVersion = _gameVersion.Length > 0 ? _gameVersion : "1.21.4";

            IReadOnlyList<Modrinth.ProjectHit> hits;
            if (_category == ModrinthCategory.Mods)
            {
                var facets = $"[[\"versions:{gameVersion}\"],[\"categories:{_loader}\"]]";
                hits = await Modrinth.SearchByFacetsAsync(query, facets, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }
            else if (_category == ModrinthCategory.Modpacks)
            {
                // 整合包只按游戏版本筛选：加载器由整合包自己声明
                var facets = $"[[\"project_type:modpack\"],[\"versions:{gameVersion}\"]]";
                hits = await Modrinth.SearchByFacetsAsync(query, facets, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                var type = _category == ModrinthCategory.Shaders ? "shader" : "resourcepack";
                var facets = $"[[\"project_type:{type}\"],[\"versions:{gameVersion}\"]]";
                hits = await Modrinth.SearchByFacetsAsync(query, facets, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }

            Rows.Clear();
            foreach (var hit in hits)
                Rows.Add(new RepoRow(hit, this));

            if (hits.Count == 0)
            {
                Status = browsing
                    ? Loc.T("暂时没有取到热门内容 · 稍后重试,或直接搜索关键词")
                    : Loc.T("没有匹配结果 · 换个关键词或确认实例版本");
            }
            else
            {
                Status = string.Format(Loc.T("共 {0} 个结果 · {1} · {2}"), hits.Count, gameVersion, _loader);
            }

            OnBrowseState();
        }
        catch (Exception ex)
        {
            AppLog.Error("RepoPage", ex);
            Status = Loc.T("检索失败:") + ex.Message;
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("检索失败:") + ex.Message);
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>
    /// 整合包一键导入：下载 .mrpack → 读清单 → (可选)自动创建匹配实例 → 导入 → 提示立即启动。
    /// </summary>
    private async Task InstallModpackAsync(RepoRow row)
    {
        string? zipPath = null;
        try
        {
            Status = string.Format(Loc.T("正在解析 {0} 的最新文件…"), row.Title);
            var file = await Modrinth.LatestVersionFileAsync(row.Hit.Slug, CancellationToken.None)
                .ConfigureAwait(true);

            if (file is null)
            {
                Status = string.Format(Loc.T("{0} 没有可用的整合包文件"), row.Title);
                AppServices.Notify.Publish(NotifyKind.Warning, Status);
                return;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "sl-modpack");
            Directory.CreateDirectory(tempDir);
            zipPath = Path.Combine(tempDir, file.FileName);

            Status = string.Format(Loc.T("正在下载 {0}（{1}）…"), file.FileName, file.SizeText);
            using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(20) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SalmonLauncher/" + SL.Core.Boot.Runtime.AppVersion);
                using var response = await http
                    .GetAsync(file.Url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(true);
                response.EnsureSuccessStatusCode();

                await using var target = File.Create(zipPath);
                await response.Content.CopyToAsync(target).ConfigureAwait(true);
            }

            ModpackManifest manifest;
            try
            {
                manifest = ModpackImporter.Inspect(zipPath);
            }
            catch (Exception ex)
            {
                Status = Loc.T("读取整合包清单失败：") + ex.Message;
                AppServices.Notify.Publish(NotifyKind.Error, Status);
                return;
            }

            Status = string.Format(Loc.T("{0}：{1} 个模组 · {2} · {3}"),
                manifest.Name, manifest.ModCount, manifest.GameVersion, manifest.Loader);
            AppLog.Info("RepoPage",
                $"整合包 {manifest.Name} 需要 {manifest.GameVersion}/{manifest.Loader}，模组 {manifest.ModCount} 个");

            // 目标实例：版本不匹配时提供自动创建
            var targetId = _targetInstance;
            var mismatch = manifest.GameVersion.Length > 0
                           && !string.Equals(_gameVersion, manifest.GameVersion, StringComparison.OrdinalIgnoreCase);

            if (mismatch && manifest.NeedsLoader && manifest.GameVersion.Length > 0)
            {
                var create = await AppServices.Dialogs.ConfirmAsync(
                    Loc.T("整合包需要其它版本"),
                    string.Format(Loc.T("该整合包需要 {0} · {1}，与当前实例（{2} · {3}）不一致。\n\n选「创建并导入」会自动补齐原版、安装加载器并建立新实例。"),
                        manifest.GameVersion, manifest.Loader, _gameVersion, _loader),
                    MsgMark.Question,
                    Loc.T("创建并导入"),
                    Loc.T("导入到当前实例")).ConfigureAwait(true);

                if (create)
                {
                    Status = Loc.T("正在补齐原版文件…");
                    await VanillaInstaller.EnsureAsync(manifest.GameVersion, null, CancellationToken.None,
                        text => _ui.TryEnqueue(() => Status = text)).ConfigureAwait(true);

                    var vanillaJson = Path.Combine(SL.Core.Boot.Runtime.InstancesDir, manifest.GameVersion,
                        "meta", manifest.GameVersion + ".json");
                    var vanillaJar = Path.Combine(SL.Core.Boot.Runtime.InstancesDir, manifest.GameVersion,
                        "versions", manifest.GameVersion + ".jar");

                    var plan = await LoaderInstall.BuildPlanAsync(
                        manifest.Loader,
                        manifest.GameVersion,
                        manifest.LoaderVersion.Length > 0 ? manifest.LoaderVersion : null,
                        vanillaJson,
                        vanillaJar,
                        CancellationToken.None,
                        text => _ui.TryEnqueue(() => Status = text)).ConfigureAwait(true);

                    targetId = plan.InstanceId;
                    AppLog.Info("RepoPage", "已创建实例 " + targetId);
                }
            }

            if (string.IsNullOrWhiteSpace(targetId))
            {
                await AppServices.Dialogs.AlertAsync(
                    Loc.T("尚未安装实例"),
                    Loc.T("请先安装一个版本，或让启动器按整合包要求自动创建实例。"),
                    MsgMark.Info).ConfigureAwait(true);
                return;
            }

            Status = Loc.T("正在导入整合包…");
            var result = await ModpackImporter.ImportAsync(zipPath, targetId!,
                text => _ui.TryEnqueue(() => Status = text), CancellationToken.None).ConfigureAwait(true);

            var total = result.Imported + result.Downloaded;
            AppLog.Info("RepoPage",
                $"整合包 {manifest.Name} → {targetId}：解压 {result.Imported} / 下载 {result.Downloaded} / 跳过 {result.Skipped} / 失败 {result.Failed}");

            AppServices.Notify.Publish(
                result.Failed == 0 ? NotifyKind.Success : NotifyKind.Warning,
                string.Format(Loc.T("导入完成：{0} 个文件，{1} 个失败"), total, result.Failed));

            Status = string.Format(Loc.T("已导入到实例 {0}（{1} 个文件）"), targetId, total);

            // 刷新实例列表并切到该实例
            try
            {
                SL.App.Instances.InstanceContext.Refresh();
                SL.App.Instances.InstanceContext.Select(targetId!);
            }
            catch (Exception ex)
            {
                AppLog.Warn("RepoPage", "刷新实例列表失败：" + ex.Message);
            }

            var launchNow = await AppServices.Dialogs.ConfirmAsync(
                Loc.T("导入完成"),
                string.Format(Loc.T("整合包已导入到实例 {0}。\n\n要现在启动游戏吗？"), targetId),
                MsgMark.Question,
                Loc.T("立即启动"),
                Loc.T("稍后")).ConfigureAwait(true);

            if (launchNow)
                AppServices.Nav.Navigate("launch.home");
        }
        catch (Exception ex)
        {
            AppLog.Error("RepoPage", ex);
            Status = Loc.T("导入整合包失败：") + ex.Message;
            AppServices.Notify.Publish(NotifyKind.Error, Status);
        }
        finally
        {
            if (zipPath is not null)
            {
                try
                {
                    if (File.Exists(zipPath))
                        File.Delete(zipPath);
                }
                catch
                {
                    // 临时文件清理失败不影响主流程
                }
            }
        }
    }

    internal async Task InstallAsync(RepoRow row)
    {
        // 整合包不是单文件下载，而是整包导入
        if (_category == ModrinthCategory.Modpacks)
        {
            await InstallModpackAsync(row).ConfigureAwait(true);
            return;
        }

        if (_targetInstance.Length == 0)
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("尚未安装实例"),
                Loc.T("请先在「资源 → 游戏版本」安装一个实例;模组、光影与资源包都需要放进某个实例目录。"),
                MsgMark.Info).ConfigureAwait(true);
            return;
        }

        try
        {
            Status = string.Format(Loc.T("正在解析 {0} 的最新文件…"), row.Title);
            var file = _category == ModrinthCategory.Mods
                ? await Modrinth.LatestVersionFileAsync(row.Hit.Slug, _gameVersion, _loader, CancellationToken.None)
                    .ConfigureAwait(true)
                : await Modrinth.LatestVersionFileAsync(row.Hit.Slug, CancellationToken.None).ConfigureAwait(true);

            if (file is null)
            {
                Status = string.Format(Loc.T("{0} 没有适用于 {1} / {2} 的文件"), row.Title, _gameVersion, _loader);
                AppServices.Notify.Publish(NotifyKind.Warning, string.Format(Loc.T("{0} 没有适用于当前版本的文件"), row.Title));
                return;
            }

            InstallWizard.SubmitInstanceFile(_targetInstance, SubDir, file.FileName,
                file.Url, file.Sha1, file.Size);
            AppLog.Info("RepoPage", $"已提交 {row.Title} → {SubDir}/{file.FileName}");
            Status = string.Format(Loc.T("已提交 {0}({1})到 {2}"), file.FileName, file.SizeText, SubDir);
            AppServices.Notify.Publish(NotifyKind.Success, string.Format(Loc.T("已提交下载:{0}"), file.FileName));
        }
        catch (Exception ex)
        {
            AppLog.Error("RepoPage", ex);
            Status = Loc.T("安装失败:") + ex.Message;
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("安装失败:") + ex.Message);
        }
    }
}