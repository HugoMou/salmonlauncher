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
using System.Security.Cryptography;

namespace SL.Core.Launch;

/// <summary>
/// Ensures a vanilla version is on disk (metadata + core jar + libraries + asset index).
///
/// Why this exists: loader installation used to require a *manually* installed vanilla first, which
/// forced users into "install vanilla → go back → install loader". The install wizard can now ask for
/// a loader directly and let this class fetch whatever is missing in one pass.
/// </summary>
public static class VanillaInstaller
{
    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SalmonLauncher/" + Boot.Runtime.AppVersion);
        return client;
    }

    /// <summary>Paths of the vanilla files a loader build needs.</summary>
    public sealed record Result(string InstanceId, string MetaPath, string JarPath, int FilesDownloaded);

    /// <summary>True when both the version metadata and a non-empty core jar already exist.</summary>
    public static bool IsReady(string gameId)
    {
        var meta = Path.Combine(Boot.Runtime.InstancesDir, gameId, "meta", gameId + ".json");
        var jar = Path.Combine(Boot.Runtime.InstancesDir, gameId, "versions", gameId + ".jar");
        return File.Exists(meta) && File.Exists(jar) && new FileInfo(jar).Length > 0;
    }

    /// <summary>
    /// Download whatever part of the vanilla version is missing. Safe to call when everything is already
    /// present (it then returns immediately) — that is what makes "install loader directly" possible.
    /// </summary>
    public static async Task<Result> EnsureAsync(
        string gameId, string? versionUrl, CancellationToken ct, Action<string>? step = null)
    {
        var instanceDir = Path.Combine(Boot.Runtime.InstancesDir, gameId);
        var metaDir = Path.Combine(instanceDir, "meta");
        var versionDir = Path.Combine(instanceDir, "versions");
        var metaPath = Path.Combine(metaDir, gameId + ".json");
        var jarPath = Path.Combine(versionDir, gameId + ".jar");

        // Fast path: nothing to do. Avoids a version-manifest round trip on the common
        // "vanilla already installed, now add a loader" flow.
        if (IsReady(gameId))
            return new Result(gameId, metaPath, jarPath, 0);

        Directory.CreateDirectory(metaDir);
        Directory.CreateDirectory(versionDir);

        if (string.IsNullOrWhiteSpace(versionUrl))
        {
            step?.Invoke($"正在查找 Minecraft {gameId} 的版本信息…");
            var versions = await Net.MojangMeta.GetVersionsAsync(false, ct).ConfigureAwait(false);
            versionUrl = versions.FirstOrDefault(v => v.Id == gameId)?.Url;
            if (string.IsNullOrWhiteSpace(versionUrl))
                throw new InvalidOperationException($"官方版本清单里没有 {gameId}");
        }

        var downloaded = 0;

        if (!File.Exists(metaPath))
        {
            step?.Invoke("正在下载版本元数据…");
            var json = await GetStringAsync(versionUrl, ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(metaPath, json, ct).ConfigureAwait(false);
            downloaded++;
        }

        var detail = await Net.MojangMeta.GetVersionAsync(versionUrl, ct).ConfigureAwait(false)
                     ?? throw new InvalidOperationException("版本元数据解析失败");

        if (detail.Client is { } client && (!File.Exists(jarPath) || new FileInfo(jarPath).Length == 0))
        {
            step?.Invoke($"正在下载游戏核心 {gameId}.jar…");
            await DownloadAsync(client.Url, jarPath, client.Sha1, ct).ConfigureAwait(false);
            downloaded++;
        }

        var total = detail.Libraries.Count;
        var done = 0;
        foreach (var lib in detail.Libraries)
        {
            done++;
            var dest = Path.Combine(Boot.Runtime.LibrariesDir,
                lib.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(dest) && lib.Size > 0 && new FileInfo(dest).Length == lib.Size)
                continue;

            step?.Invoke($"正在下载库文件 {done}/{total}…");
            await DownloadAsync(lib.Url, dest, lib.Sha1, ct).ConfigureAwait(false);
            downloaded++;
        }

        if (detail.AssetIndex is { } index)
        {
            var dest = Path.Combine(Boot.Runtime.AssetsDir, "indexes", detail.AssetId + ".json");
            if (!File.Exists(dest))
            {
                step?.Invoke("正在下载资源索引…");
                await DownloadAsync(index.Url, dest, index.Sha1, ct).ConfigureAwait(false);
                downloaded++;
            }
        }

        return new Result(gameId, metaPath, jarPath, downloaded);
    }

    // ------------------------------------------------------------ networking

    /// <summary>Mirror fallbacks for the official hosts, used only when the primary URL fails.</summary>
    private static string[] MirrorsOf(string url)
    {
        var bmcl = new List<string>();
        if (url.Contains("piston-data.mojang.com", StringComparison.Ordinal))
            bmcl.Add(url.Replace("piston-data.mojang.com", "bmclapi2.bangbang93.com", StringComparison.Ordinal));
        if (url.Contains("piston-meta.mojang.com", StringComparison.Ordinal))
            bmcl.Add(url.Replace("piston-meta.mojang.com", "bmclapi2.bangbang93.com", StringComparison.Ordinal));
        if (url.Contains("launchermeta.mojang.com", StringComparison.Ordinal))
            bmcl.Add(url.Replace("launchermeta.mojang.com", "bmclapi2.bangbang93.com", StringComparison.Ordinal));
        if (url.Contains("libraries.minecraft.net/", StringComparison.Ordinal))
            bmcl.Add(url.Replace("https://libraries.minecraft.net/", "https://bmclapi2.bangbang93.com/maven/",
                StringComparison.Ordinal));
        if (url.Contains("resources.download.minecraft.net", StringComparison.Ordinal))
            bmcl.Add(url.Replace("https://resources.download.minecraft.net",
                "https://bmclapi2.bangbang93.com/assets", StringComparison.Ordinal));
        return [.. bmcl];
    }

    private static async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        var urls = new List<string> { url };
        urls.AddRange(MirrorsOf(url));

        Exception? last = null;
        foreach (var candidate in urls)
        {
            try
            {
                return await Http.GetStringAsync(candidate, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }
        throw last ?? new HttpRequestException(url);
    }

    private static async Task DownloadAsync(string url, string dest, string? sha1, CancellationToken ct)
    {
        var urls = new List<string> { url };
        urls.AddRange(MirrorsOf(url));

        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        var temp = dest + ".part";
        Exception? last = null;

        foreach (var candidate in urls)
        {
            try
            {
                await using (var source = await Http.GetStreamAsync(candidate, ct).ConfigureAwait(false))
                await using (var target = File.Create(temp))
                    await source.CopyToAsync(target, ct).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(sha1) && !Sha1Matches(temp, sha1))
                    throw new InvalidDataException("SHA-1 校验失败");

                File.Move(temp, dest, overwrite: true);
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                TryDelete(temp);
            }
        }
        throw last ?? new HttpRequestException(url);
    }

    private static bool Sha1Matches(string path, string expected)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA1.HashData(stream);
        return Convert.ToHexString(hash).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // a leftover .part is harmless; the next attempt overwrites it
        }
    }
}