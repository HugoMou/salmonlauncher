import pathlib

p = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs')
text = p.read_text(encoding='utf-8')

# Patch Configure method
old_config = '''/// <summary>The route key determines the category: dl.pack / dl.shaders / dl.packs.</summary>
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
        };'''

new_config = '''/// <summary>The route key determines the category: dl.pack / dl.shaders / dl.packs / dl.modpacks.</summary>
    public void Configure(string routeKey)
    {
        _category = routeKey switch
        {
            "dl.shaders" => ModrinthCategory.Shaders,
            "dl.packs" => ModrinthCategory.ResourcePacks,
            "dl.modpacks" => ModrinthCategory.Modpacks,
            _ => ModrinthCategory.Mods,
        };
        Title = _category switch
        {
            ModrinthCategory.Mods => Loc.T("模组仓库"),
            ModrinthCategory.Shaders => Loc.T("光影仓库"),
            ModrinthCategory.ResourcePacks => Loc.T("资源包仓库"),
            ModrinthCategory.Modpacks => Loc.T("整合包仓库"),
        };'''

text = text.replace(old_config, new_config, 1)
print('Done Configure')

# Patch SearchAsync - add Modpacks case
old_search = '''IReadOnlyList<Modrinth.ProjectHit> hits;
            if (_category == ModrinthCategory.Mods)
            {
                var facets = $"[[\"versions:{gameVersion}\"],[\"categories:{_loader}\"]]";
                hits = await Modrinth.SearchByFacetsAsync(query, facets, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                var type = _category == ModrinthCategory.Shaders ? "shader" : "resourcepack";
                var facets = $"[[\"project_type:{type}\"],[\"versions:{gameVersion}\"]]";
                hits = await Modrinth.SearchByFacetsAsync(query, facets, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }'''

new_search = '''IReadOnlyList<Modrinth.ProjectHit> hits;
            if (_category == ModrinthCategory.Mods)
            {
                var facets = $"[[\"versions:{gameVersion}\"],[\"categories:{_loader}\"]]";
                hits = await Modrinth.SearchByFacetsAsync(query, facets, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }
            else if (_category == ModrinthCategory.Modpacks)
            {
                hits = await Modrinth.SearchModpacksAsync(query, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                var type = _category == ModrinthCategory.Shaders ? "shader" : "resourcepack";
                var facets = $"[[\"project_type:{type}\"],[\"versions:{gameVersion}\"]]";
                hits = await Modrinth.SearchByFacetsAsync(query, facets, sort, _browseOffset, CancellationToken.None).ConfigureAwait(true);
            }'''

text = text.replace(old_search, new_search, 1)
print('Done SearchAsync')

# Patch InstallAsync
old_install = '''internal async Task InstallAsync(RepoRow row)
        {
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
    }'''

new_install = '''internal async Task InstallAsync(RepoRow row)
        {
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
                Modrinth.VersionFile? file;
                if (_category == ModrinthCategory.Mods)
                {
                    file = await Modrinth.LatestVersionFileAsync(row.Hit.Slug, _gameVersion, _loader, CancellationToken.None)
                        .ConfigureAwait(true);
                }
                else if (_category == ModrinthCategory.Modpacks)
                {
                    file = await Modrinth.LatestVersionFileAsync(row.Hit.Slug, CancellationToken.None)
                        .ConfigureAwait(true);
                }
                else
                {
                    file = await Modrinth.LatestVersionFileAsync(row.Hit.Slug, CancellationToken.None)
                        .ConfigureAwait(true);
                }

                if (file is null)
                {
                    Status = string.Format(Loc.T("{0} 没有适用于 {1} / {2} 的文件"), row.Title, _gameVersion, _loader);
                    AppServices.Notify.Publish(NotifyKind.Warning, string.Format(Loc.T("{0} 没有适用于当前版本的文件"), row.Title));
                    return;
                }

                if (_category == ModrinthCategory.Modpacks)
                {
                    // Download the .mrpack and hand off to the pack importer
                    var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), file.FileName);
                    await DownloadModpackAsync(file.Url, tempPath, row.Title).ConfigureAwait(true);
                    await AppServices.Dialogs.AlertAsync(Loc.T("整合包已下载"),
                        string.Format(Loc.T("{0} 已下载到临时目录,请用「导入整合包」功能导入"), row.Title), MsgMark.Info).ConfigureAwait(true);
                }
                else
                {
                    InstallWizard.SubmitInstanceFile(_targetInstance, SubDir, file.FileName,
                        file.Url, file.Sha1, file.Size);
                    AppLog.Info("RepoPage", $"已提交 {row.Title} → {SubDir}/{file.FileName}");
                    Status = string.Format(Loc.T("已提交 {0}({1})到 {2}"), file.FileName, file.SizeText, SubDir);
                    AppServices.Notify.Publish(NotifyKind.Success, string.Format(Loc.T("已提交下载:{0}"), file.FileName));
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("RepoPage", ex);
                Status = Loc.T("安装失败:") + ex.Message;
                AppServices.Notify.Publish(NotifyKind.Error, Loc.T("安装失败:") + ex.Message);
            }
        }

        private async Task DownloadModpackAsync(string url, string destPath, string title)
        {
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SalmonLauncher/0.3");
                using var stream = await http.GetStreamAsync(url).ConfigureAwait(false);
                await using var file = System.IO.File.Create(destPath);
                await stream.CopyToAsync(file).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLog.Error("RepoPage", ex);
                AppServices.Notify.Publish(NotifyKind.Error, Loc.T("下载失败:") + ex.Message);
            }
        }
    }'''

text = text.replace(old_install, new_install, 1)
print('Done InstallAsync')

pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs').write_text(text, encoding='utf-8')
print('All patches applied')