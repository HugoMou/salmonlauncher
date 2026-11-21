import pathlib

p = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs')
text = p.read_text(encoding='utf-8')

# Find the exact block to replace - from "if (file is null)" to the end of the class
old = '''            if (file is null)
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

new = '''            if (file is null)
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

text = pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs').read_text(encoding='utf-8')

old = '''            if (file is null)
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

if old in text:
    text = text.replace(old, new, 1)
    print("✓ InstallAsync rest patched")
else:
    print("✗ NOT FOUND")
    # Debug: find the exact text
    idx = text.find('if (file is null)')
    if idx >= 0:
        print(f"Found at {idx}")
        print(repr(text[idx:idx+500]))
    else:
        print("Not found at all")

pathlib.Path(r'D:\SalmonLauncher\src\SL.App\Pages\RepoVM.cs').write_text(text.replace(old, new, 1), encoding='utf-8')
print("All patches applied")