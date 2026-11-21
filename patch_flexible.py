import pathlib
import re

p = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs')
text = p.read_text(encoding='utf-8')

# ============================================================
# 1. SearchHint - add Modpacks case (flexible whitespace)
# ============================================================
old_search_hint = r'''public string SearchHint => _category switch
\s*\{\s*
ModrinthCategory\.Mods => Loc\.T\("搜索模组\(如 sodium、create\)"\),\s*
ModrinthCategory\.Shaders => Loc\.T\("搜索光影\(如 complementary、bsl\)"\),\s*
_ => Loc\.T\("搜索资源包\(如 faithful、stay true\)"\)\s*
\};'''

new_search_hint = '''public string SearchHint => _category switch
    {
        ModrinthCategory.Mods => Loc.T("搜索模组(如 sodium、create)"),
        ModrinthCategory.Shaders => Loc.T("搜索光影(如 complementary、bsl)"),
        ModrinthCategory.ResourcePacks => Loc.T("搜索资源包(如 faithful、stay true)"),
        ModrinthCategory.Modpacks => Loc.T("搜索整合包(如 ATM、RLCraft、Create)"),
    };'''

# ============================================================
# 2. SubDir - add Modpacks case
# ============================================================
old_subdir = r'''public string SubDir => _category switch
\s*\{\s*
ModrinthCategory\.Mods => "mods",\s*
ModrinthCategory\.Shaders => "shaderpacks",\s*
_ => "resourcepacks",\s*
\};'''

new_subdir = '''public string SubDir => _category switch
    {
        ModrinthCategory.Mods => "mods",
        ModrinthCategory.Shaders => "shaderpacks",
        ModrinthCategory.ResourcePacks => "resourcepacks",
        ModrinthCategory.Modpacks => "modpacks",
    };'''

# ============================================================
# 3. Configure method - add dl.modpacks route
# ============================================================
old_config = r'''/// <summary>The route key determines the category: dl\.pack / dl\.shaders / dl\.packs\.</summary>
\s*public void Configure\(string routeKey\)
\s*\{
\s*_category = routeKey switch
\s*\{\s*
"dl\.shaders" => ModrinthCategory\.Shaders,\s*
"dl\.packs" => ModrinthCategory\.ResourcePacks,\s*
_ => ModrinthCategory\.Mods,\s*
\};
\s*Title = _category switch
\s*\{\s*
ModrinthCategory\.Mods => Loc\.T\("模组仓库"\),\s*
ModrinthCategory\.Shaders => Loc\.T\("光影仓库"\),\s*
_ => Loc\.T\("资源包仓库"\),\s*
\};'''

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

# ============================================================
# 4. SearchAsync - add Modpacks search path
# ============================================================
old_search = r'''IReadOnlyList<Modrinth\.ProjectHit> hits;
\s*if \(_category == ModrinthCategory\.Mods\)
\s*\{\s*
var facets = \$\"\\[\"versions:\{gameVersion\}"\],\[\"categories:\{_loader\}"\]\]"\s*;
hits = await Modrinth\.SearchByFacetsAsync\(query, facets, sort, _browseOffset, CancellationToken\.None\)\.ConfigureAwait\(true\);\s*
\}
else
\s*\{\s*
var type = _category == ModrinthCategory\.Shaders \? "shader" : "resourcepack";
var facets = \$\"\\[\"project_type:\{type\}"\],\[\"versions:\{gameVersion\}"\]\]"\s*;
hits = await Modrinth\.SearchByFacetsAsync\(query, facets, sort, _browseOffset, CancellationToken\.None\)\.ConfigureAwait\(true\);\s*
\}'''

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

# ============================================================
# 5. InstallAsync - add Modpacks install path
# ============================================================
old_install = r'''internal async Task InstallAsync\(RepoRow row\)
\s*\{
\s*if \(_targetInstance\.Length == 0\)
\s*\{
\s*await AppServices\.Dialogs\.AlertAsync\(Loc\.T\("尚未安装实例"\),
\s*Loc\.T\("请先在「资源 → 游戏版本」安装一个实例;模组、光影与资源包都需要放进某个实例目录。"\),
\s*MsgMark\.Info\)\.ConfigureAwait\(true\);
\s*return;
\s*\}
\s*try
\s*\{\s*
Status = string\.Format\(Loc\.T\("正在解析 \{0\} 的最新文件…"\), row\.Title\);
var file = _category == ModrinthCategory\.Mods
\s*\? await Modrinth\.LatestVersionFileAsync\(row\.Hit\.Slug, _gameVersion, _loader, CancellationToken\.None\)
\s*\.\ConfigureAwait\(true\)
\s*:\s*await Modrinth\.LatestVersionFileAsync\(row\.Hit\.Slug, CancellationToken\.None\)\.ConfigureAwait\(true\);

if \(file is null\)
\s*\{\s*
Status = string\.Format\(Loc\.T\("\{0\} 没有适用于 \{1\} / \{2\} 的文件"\), row\.Title, _gameVersion, _loader\);
AppServices\.Notify\.Publish\(NotifyKind\.Warning, string\.Format\(Loc\.T\("\{0\} 没有适用于当前版本的文件"\), row\.Title\)\)\s*;
return;
\s*\}

InstallWizard\.SubmitInstanceFile\(_targetInstance, SubDir, file\.FileName,
\s*file\.Url, file\.Sha1, file\.Size\);
AppLog\.Info\("RepoPage", \$\"已提交 \{row\.Title\} → \{SubDir\}/\{file\.FileName\}"\);
Status = string\.Format\(Loc\.T\("已提交 \{0\}\(\{1\}\)到 \{2\}"\), file\.FileName, file\.SizeText, SubDir\);
AppServices\.Notify\.Publish\(NotifyKind\.Success, string\.Format\(Loc\.T\("已提交下载:\{0\}"\), file\.FileName\)\)\s*;
\}
catch \(Exception ex\)
\s*\{\s*
AppLog\.Error\("RepoPage", ex\);
Status = Loc\.T\("安装失败:"\) \+ ex\.Message;
AppServices\.Notify\.Publish\(NotifyKind\.Error, Loc\.T\("安装失败:"\) \+ ex\.Message\);
\s*\}
\s*\}\s*\}\s*\}'''

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
    }
}'''

# Apply all patches
text = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs').read_text(encoding='utf-8')

patterns = [
    ("SearchHint", old_search_hint, new_search_hint),
    ("SubDir", old_subdir, new_subdir),
    ("Configure", old_config, new_config),
    ("SearchAsync", old_search, new_search),
    ("InstallAsync", old_install, new_install),
]

for name, old, new in [
    ("SearchHint", old_search_hint, new_search_hint),
    ("SubDir", old_subdir, new_subdir),
    ("Configure", old_config, new_config),
    ("SearchAsync", old_search, new_search),
    ("InstallAsync", old_install, new_install),
]:
    match = re.search(old, text, re.DOTALL)
    if match:
        text = text[:match.start()] + new + text[match.end():]
        print(f"✓ {name} patched")
    else:
        print(f"✗ {name} NOT FOUND")

pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs').write_text(text, encoding='utf-8')
print("All patches applied")