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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using SL.Core.Logging;

namespace SL.App.Infrastructure;

/// <summary>A published SalmonLauncher release with its installer package.</summary>
public sealed record LauncherRelease(
    string Tag,
    string Version,
    string Name,
    string Notes,
    Uri PageUrl,
    Uri AssetApiUrl,
    Uri BrowserUrl,
    string FileName,
    string Sha256,
    long Size,
    DateTimeOffset PublishedAt);

/// <summary>
/// Checks the project's own GitHub releases and downloads the installer for self-update.
/// Only the installer package is used: it keeps user data under %APPDATA%\SalmonLauncher, so
/// settings, accounts, instances and saves survive the upgrade.
/// </summary>
public static class LauncherUpdater
{
    /// <summary>GitHub repository that publishes launcher builds.</summary>
    public const string Repository = "HugoMou/salmonlauncher";

    public const string ReleasesUrl = $"https://github.com/{Repository}/releases";
    public const string LatestPageUrl = $"https://github.com/{Repository}/releases/latest";
    public const string RepositoryUrl = $"https://github.com/{Repository}";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(20),
        })
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"SalmonLauncher/{Core.Boot.Runtime.AppVersion}");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    /// <summary>Current running version.</summary>
    public static Version CurrentVersion =>
        Version.TryParse(Core.Boot.Runtime.AppVersion, out var v) ? v : new Version(2, 0, 0);

    /// <summary>Downloaded installers live here, outside the program folder.</summary>
    public static string UpdateDirectory => Path.Combine(Core.Boot.Runtime.Root, "update");

    /// <summary>Ask GitHub for the newest release that carries an installer package.</summary>
    public static async Task<LauncherRelease> GetLatestAsync(CancellationToken ct)
    {
        using var response = await Http.GetAsync(
            $"https://api.github.com/repos/{Repository}/releases?per_page=10",
            HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("GitHub 接口暂时不可用（可能触发了调用频率限制），请稍后再试。");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
                continue;
            if (release.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True)
                continue;

            var tag = release.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(tag))
                continue;

            if (!release.TryGetProperty("assets", out var assets))
                continue;

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                // Installer package only; portable builds are never auto-installed.
                if (!name.StartsWith("SL-Setup-", StringComparison.OrdinalIgnoreCase)
                    || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    continue;

                var apiUrl = asset.TryGetProperty("url", out var u) ? u.GetString() : null;
                var browserUrl = asset.TryGetProperty("browser_download_url", out var b) ? b.GetString() : null;
                if (string.IsNullOrWhiteSpace(apiUrl) || string.IsNullOrWhiteSpace(browserUrl))
                    continue;

                var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
                var sha256 = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                    ? digest["sha256:".Length..]
                    : "";
                var size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var value) ? value : 0L;

                var notes = release.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
                var title = release.TryGetProperty("name", out var rn) ? rn.GetString() ?? tag : tag;
                var page = release.TryGetProperty("html_url", out var hu) ? hu.GetString() ?? ReleasesUrl : ReleasesUrl;
                var published = release.TryGetProperty("published_at", out var pa)
                                && pa.TryGetDateTimeOffset(out var when)
                    ? when
                    : DateTimeOffset.MinValue;

                return new LauncherRelease(
                    tag,
                    tag.TrimStart('v', 'V'),
                    title,
                    notes,
                    new Uri(page),
                    new Uri(apiUrl),
                    new Uri(browserUrl),
                    name,
                    sha256,
                    size,
                    published);
            }
        }

        throw new InvalidOperationException("GitHub 发布页中没有找到可用的安装包（SL-Setup-*.exe）。");
    }

    /// <summary>Whether <paramref name="release"/> is newer than the running build.</summary>
    public static bool IsNewer(LauncherRelease release)
        => Version.TryParse(release.Version, out var published) && Normalize(published) > Normalize(CurrentVersion);

    private static Version Normalize(Version version)
        => new(version.Major, version.Minor, version.Build < 0 ? 0 : version.Build);

    /// <summary>Download the installer into the update folder; returns its path.</summary>
    public static async Task<string> DownloadInstallerAsync(
        LauncherRelease release,
        IProgress<DownloadProgressInfo>? progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(UpdateDirectory);
        var target = Path.Combine(UpdateDirectory, release.FileName);
        RangedDownloader.TryDelete(target);

        var browser = release.BrowserUrl.ToString();
        var sources = new List<DownloadSource>
        {
            new($"https://gh-proxy.com/{browser}", "gh-proxy.com"),
            new(release.AssetApiUrl.ToString(), "GitHub API"),
            new($"https://ghproxy.net/{browser}", "ghproxy.net"),
            new(browser, "GitHub"),
        };

        try
        {
            await RangedDownloader
                .DownloadAsync(sources, release.Size, release.Sha256, target, progress, ct)
                .ConfigureAwait(false);
            return target;
        }
        catch
        {
            RangedDownloader.TryDelete(target);
            throw;
        }
    }

    /// <summary>
    /// Launch the downloaded installer. The installer upgrades the program folder in place and never
    /// touches %APPDATA%\SalmonLauncher, so settings, accounts, instances and saves are preserved.
    /// </summary>
    public static void RunInstaller(string installerPath, bool silent)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException("安装包不存在。", installerPath);

        var arguments = silent ? "/SILENT /NORESTART" : "/NORESTART";
        var startInfo = new ProcessStartInfo(installerPath, arguments)
        {
            UseShellExecute = true,
            WorkingDirectory = UpdateDirectory,
        };

        var started = Process.Start(startInfo);
        AppLog.Info("Update", started is null
            ? "安装包启动失败"
            : $"已启动安装包 {Path.GetFileName(installerPath)}（{arguments}）");
    }

    /// <summary>Delete stale installers left in the update folder.</summary>
    public static void ClearOldInstallers(string? keep = null)
    {
        try
        {
            if (!Directory.Exists(UpdateDirectory))
                return;
            foreach (var file in Directory.EnumerateFiles(UpdateDirectory, "*.exe"))
            {
                if (keep is not null && string.Equals(file, keep, StringComparison.OrdinalIgnoreCase))
                    continue;
                RangedDownloader.TryDelete(file);
            }
        }
        catch
        {
            // Cleanup is best-effort.
        }
    }

    /// <summary>Shorten release notes for display (GitHub markdown is shown as plain text).</summary>
    public static string Summarize(string notes, int maxLength = 4000)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return "";
        var text = notes.Replace("\r\n", "\n").Trim();
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }
}
