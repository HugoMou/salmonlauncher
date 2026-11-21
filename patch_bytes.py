import pathlib

p = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs')
text = p.read_text(encoding='utf-8')

# Find the exact position
idx = text.find('if (file is null)')
print(f"Found at {idx}")

# Find the end of the InstallAsync method (the closing brace of the class)
class_end = text.rfind('    }\n}')
print(f"Class end at {class_end}")

# Extract the exact block
start = idx
end = class_end + 2  # include the closing braces

old_block = text[idx:end]
print(f"Block length: {len(old_block)}")
print("First 200 chars:")
print(repr(old_block[:200]))
print("...")
print("Last 200 chars:")
print(repr(old_block[-200:]))

# New block
new_block = '''            if (file is null)
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

# Apply replacement
text = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs').read_text(encoding='utf-8')

idx = text.find('if (file is null)')
class_end = text.rfind('    }\n}')

if idx >= 0:
    new_text = text[:idx] + '''            if (file is null)
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

    pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs').write_text(text[:idx] + new_text, encoding='utf-8')
    print("✓ Replaced!")