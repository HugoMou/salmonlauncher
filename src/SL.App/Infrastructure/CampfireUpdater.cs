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

/// <summary>A published Campfire release, as described by the project's own release page.</summary>
public sealed record CampfireRelease(
    string Tag,
    string Version,
    Uri AssetApiUrl,
    Uri BrowserUrl,
    Uri PageUrl,
    string Sha256,
    long Size);

/// <summary>
/// Reads and downloads Campfire from its upstream GitHub releases.
/// The launcher only ever fetches the unmodified release binary and checks it against the
/// SHA-256 GitHub publishes for that asset; nothing is repacked or patched.
/// </summary>
public static class CampfireUpdater
{
    public const string Repository = "Xocio/Campfire";

    /// <summary>Where the bundled program lives: next to the launcher executable.</summary>
    public static string ProgramPath => Path.Combine(AppContext.BaseDirectory, "campfire", "Campfire.exe");

    public static string ProgramDirectory => Path.GetDirectoryName(ProgramPath) ?? AppContext.BaseDirectory;

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

    /// <summary>Version reported by the local Campfire.exe, or null when it is absent/unreadable.</summary>
    public static Version? ReadInstalledVersion()
    {
        try
        {
            if (!File.Exists(ProgramPath))
                return null;
            var text = FileVersionInfo.GetVersionInfo(ProgramPath).FileVersion;
            return Version.TryParse(text, out var version) ? version : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Ask the upstream release feed for the latest stable release.</summary>
    public static async Task<CampfireRelease> GetLatestAsync(CancellationToken ct)
    {
        using var response = await Http.GetAsync(
            $"https://api.github.com/repos/{Repository}/releases/latest",
            HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("GitHub 接口暂时不可用（可能触发了调用频率限制），请稍后再试。");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = document.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() ?? "" : "";
        var page = root.TryGetProperty("html_url", out var pageElement) ? pageElement.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(tag))
            throw new InvalidOperationException("发布页返回的版本号为空。");

        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                if (!string.Equals(name, "Campfire.exe", StringComparison.OrdinalIgnoreCase))
                    continue;

                var apiUrl = asset.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(apiUrl))
                    continue;

                var browserUrl = asset.TryGetProperty("browser_download_url", out var browserElement)
                    ? browserElement.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(browserUrl))
                    browserUrl = $"https://github.com/{Repository}/releases/download/{tag}/Campfire.exe";

                var digest = asset.TryGetProperty("digest", out var digestElement) ? digestElement.GetString() ?? "" : "";
                var sha256 = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                    ? digest["sha256:".Length..]
                    : "";
                var size = asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var value)
                    ? value
                    : 0L;

                return new CampfireRelease(
                    tag,
                    tag.TrimStart('v', 'V'),
                    new Uri(apiUrl),
                    new Uri(browserUrl!),
                    new Uri(string.IsNullOrWhiteSpace(page)
                        ? $"https://github.com/{Repository}/releases"
                        : page),
                    sha256,
                    size);
            }
        }

        throw new InvalidOperationException("最新发布中没有找到 Campfire.exe。");
    }

    /// <summary>
    /// Download the release binary next to the installed one and verify its SHA-256.
    /// Returns the temporary path; the caller replaces the installed program once it is no longer running.
    /// </summary>
    public static async Task<string> DownloadAsync(
        CampfireRelease release,
        IProgress<DownloadProgressInfo>? progress,
        CancellationToken ct)
    {
        var temp = Path.Combine(ProgramDirectory, "Campfire.exe.download");
        RangedDownloader.TryDelete(temp);

        var sources = new List<DownloadSource>();
        var browser = release.BrowserUrl.ToString();
        // Mirrors first: a single GitHub connection is heavily throttled on many networks.
        sources.Add(new DownloadSource($"https://gh-proxy.com/{browser}", "gh-proxy.com"));
        sources.Add(new DownloadSource(release.AssetApiUrl.ToString(), "GitHub API"));
        sources.Add(new DownloadSource($"https://ghproxy.net/{browser}", "ghproxy.net"));
        sources.Add(new DownloadSource(browser, "GitHub"));

        try
        {
            await RangedDownloader
                .DownloadAsync(sources, release.Size, release.Sha256, temp, progress, ct)
                .ConfigureAwait(false);
            return temp;
        }
        catch
        {
            RangedDownloader.TryDelete(temp);
            throw;
        }
    }

    /// <summary>Replace the installed program with a verified download.</summary>
    public static void InstallDownloaded(string temporaryFile)
    {
        Directory.CreateDirectory(ProgramDirectory);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(temporaryFile, ProgramPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                // The old file may still be releasing its lock right after the process exits.
                Thread.Sleep(300);
            }
        }
    }

    public static void TryDelete(string path) => RangedDownloader.TryDelete(path);
}
