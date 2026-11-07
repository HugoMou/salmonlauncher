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
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace SL.Core.Link.EasyTier;

/// <summary>
/// Finds or fetches the EasyTier core, the program that actually builds the virtual network a room runs on.
/// The core is Apache-2.0 and is never linked into the launcher; it is either shipped next to the executable
/// (the installer does that) or downloaded once into the launcher's data directory.
/// </summary>
public static class EasyTierRuntime
{
    /// <summary>Version of the core this launcher speaks to; kept in one place so downloads stay reproducible.</summary>
    public const string Version = "2.6.4";

    private const string ReleaseBase = "https://github.com/EasyTier/EasyTier/releases/download";

    /// <summary>Mirrors tried in order; the first two serve GitHub releases from inside mainland China.</summary>
    private static readonly string[] Mirrors =
    [
        "https://gh-proxy.com/",
        "https://ghproxy.net/",
        "",
    ];

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    /// <summary>File name the core must have after unpacking.</summary>
    private const string CoreFileName = "easytier-core.exe";

    /// <summary>Folder the installer may ship the core in, next to the launcher executable.</summary>
    public static string BundledDirectory => Path.Combine(AppContext.BaseDirectory, "easytier");

    /// <summary>A core that is ready to run, without downloading anything; <c>null</c> when none is present.</summary>
    public static string? FindLocal()
    {
        // an explicitly chosen core wins: it is how a player gets running when every mirror is unreachable
        string chosen = LinkSettings.Current.CorePath.Trim();
        if (chosen.Length > 0 && IsUsable(chosen))
            return chosen;

        foreach (string candidate in new[]
                 {
                     Path.Combine(BundledDirectory, CoreFileName),
                     Path.Combine(LinkSettings.RuntimeDirectory, CoreFileName),
                 })
        {
            if (IsUsable(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>
    /// Makes sure a usable core exists, downloading it when needed. Returns the path to <c>easytier-core.exe</c>.
    /// </summary>
    /// <param name="progress">Receives 0..1 while downloading; may be <c>null</c>.</param>
    public static async Task<string> EnsureAsync(IProgress<double>? progress, CancellationToken token)
    {
        if (FindLocal() is { } local)
            return local;

        Directory.CreateDirectory(LinkSettings.RuntimeDirectory);
        string archive = Path.Combine(LinkSettings.RuntimeDirectory, AssetName.Replace(".zip", ".download"));

        LinkLog.Info("正在下载联机内核 EasyTier " + Version + "…");
        await DownloadAsync(archive, progress, token).ConfigureAwait(false);

        try
        {
            using var zip = ZipFile.OpenRead(archive);
            foreach (var entry in zip.Entries)
            {
                string name = Path.GetFileName(entry.FullName);
                if (name.Length == 0)
                    continue;
                // only the core and its companions are needed; the archive also carries docs and a GUI
                if (!name.StartsWith("easytier-", StringComparison.OrdinalIgnoreCase)
                    && !name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                string target = Path.Combine(LinkSettings.RuntimeDirectory, name);
                entry.ExtractToFile(target, overwrite: true);
            }
        }
        catch (InvalidDataException ex)
        {
            throw new LinkException("联机内核压缩包已损坏,请重试", ex);
        }
        finally
        {
            try { File.Delete(archive); } catch { }
        }

        string core = Path.Combine(LinkSettings.RuntimeDirectory, CoreFileName);
        if (!IsUsable(core))
            throw new LinkException("联机内核解压失败,请重试");

        LinkLog.Info("联机内核已就绪:" + core);
        return core;
    }

    /// <summary>Downloads the release archive from the first mirror that answers.</summary>
    private static async Task DownloadAsync(string target, IProgress<double>? progress, CancellationToken token)
    {
        string relative = $"EasyTier/EasyTier/releases/download/v{Version}/{AssetName}";
        var mirrors = new List<string>();
        string custom = LinkSettings.Current.DownloadMirror.Trim();
        if (custom.Length > 0)
            mirrors.Add(custom.EndsWith('/') ? custom : custom + "/");
        mirrors.AddRange(Mirrors);

        Exception? last = null;
        foreach (string mirror in mirrors)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                await DownloadOneAsync(mirror + ReleaseBase + "/" + relative, target, progress, token).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                last = ex;
                LinkLog.Warn($"下载源不可用({(mirror.Length == 0 ? "github.com" : mirror)}):{ex.Message}");
                try { File.Delete(target); } catch { }
            }
        }

        throw new LinkException("联机内核下载失败:请检查网络,或在「联机设置」里指定本地内核文件", last);
    }

    /// <summary>
    /// Downloads one archive, resuming a partial file when the server supports it: the runtime is ~31 MB and
    /// mirror throughput to mainland China swings between 5 KB/s and 200 KB/s, so an interrupted transfer must
    /// not start over.
    /// </summary>
    private static async Task DownloadOneAsync(string url, string target, IProgress<double>? progress, CancellationToken token)
    {
        long already = File.Exists(target) ? new FileInfo(target).Length : 0;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (already > 0)
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(already, null);

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if (response.StatusCode is System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            try { File.Delete(target); } catch { }
            already = 0;
        }
        else if (already > 0 && response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            already = 0;   // the mirror ignored the range and is sending the whole file again
        }
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength is { } length ? length + already : -1;
        await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var destination = new FileStream(
            target, already > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long copied = already;
        while (true)
        {
            int read = await source.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read == 0)
                break;
            await destination.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            copied += read;
            if (total > 0)
                progress?.Report((double)copied / total);
        }

        await destination.FlushAsync(token).ConfigureAwait(false);
        if (total > 0 && copied < total)
            throw new IOException($"下载不完整({copied}/{total} 字节)");
    }

    /// <summary>File name of the release archive for this machine.</summary>
    private static string AssetName
    {
        get
        {
            string arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "i686",
                _ => "x86_64",
            };
            return $"easytier-windows-{arch}-v{Version}.zip";
        }
    }

    /// <summary>A core file counts as usable when it exists and looks like a real executable rather than a stub.</summary>
    private static bool IsUsable(string path)
    {
        try
        {
            return File.Exists(path) && new FileInfo(path).Length > 1024 * 1024;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
