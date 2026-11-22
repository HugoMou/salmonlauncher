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
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using SL.Core.Logging;

namespace SL.Core.Launch;

/// <summary>一个检测到的 Java 运行时。</summary>
public sealed record JavaRuntimeInfo(string Path, string Version, int MajorVersion, bool IsJdk)
{
    /// <summary>用于界面显示的简短描述。</summary>
    public string Display => $"Java {MajorVersion} · {Version}";
}

/// <summary>
/// Java 运行时检测与自动下载。
/// 优先复用系统已安装的 Java；缺失时按需从 Eclipse Adoptium(Temurin) 下载 JRE 到数据目录，
/// 不修改系统环境变量，下载的运行时只作用于本启动器拉起的游戏进程。
/// </summary>
public static class JavaRuntime
{
    /// <summary>下载镜像根地址（Adoptium 官方 API）。</summary>
    private const string AdoptiumRoot = "https://api.adoptium.net/v3/binary/latest";

    private static readonly Regex VersionRegex = new(@"version ""(\d+)(?:\.(\d+))?", RegexOptions.Compiled);

    private static IReadOnlyList<JavaRuntimeInfo>? _cache;

    /// <summary>数据目录下的运行时存放位置。</summary>
    public static string RuntimeRoot => System.IO.Path.Combine(Boot.Runtime.Root, "java");

    /// <summary>
    /// 扫描常见安装位置、PATH 与已下载目录，返回可用 Java，按主版本降序。
    /// </summary>
    public static IReadOnlyList<JavaRuntimeInfo> Detect(bool forceRefresh = false)
    {
        if (!forceRefresh && _cache is not null)
            return _cache;

        var found = new List<JavaRuntimeInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in CandidateExecutables())
        {
            if (!seen.Add(candidate))
                continue;

            var info = Probe(candidate);
            if (info is not null)
                found.Add(info);
        }

        _cache = found.OrderByDescending(i => i.MajorVersion).ThenBy(i => i.Path).ToList();
        return _cache;
    }

    /// <summary>清理缓存，下次 Detect 会重新扫描。</summary>
    public static void Invalidate() => _cache = null;

    /// <summary>
    /// 根据 Minecraft 版本号推断需要的 Java 主版本。
    /// 1.16.5 及更早使用 Java 8，1.17~1.20.4 使用 Java 17，1.20.5 及之后使用 Java 21。
    /// </summary>
    public static int RequiredMajorFor(string gameVersionId)
    {
        if (string.IsNullOrWhiteSpace(gameVersionId))
            return 21;

        // 只取纯数字开头的版本段，忽略 -fabric / -forge 等加载器后缀与快照名
        var match = Regex.Match(gameVersionId, @"^(\d+)\.(\d+)(?:\.(\d+))?");
        if (!match.Success)
            return 21;

        var major = int.Parse(match.Groups[1].Value);
        var minor = int.Parse(match.Groups[2].Value);
        var patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

        // 1.16 及之前 → 8
        if (major < 2 && (minor < 17))
            return 8;

        // 1.17 ~ 1.20.4 → 17
        if (major < 2 && (minor < 20 || (minor == 20 && patch <= 4)))
            return 17;

        // 1.20.5+ 与未来版本 → 21
        return 21;
    }

    /// <summary>找到满足该游戏版本的运行时；找不到返回 null。</summary>
    public static JavaRuntimeInfo? FindSuitable(string gameVersionId)
    {
        var required = RequiredMajorFor(gameVersionId);
        var all = Detect();
        return all.FirstOrDefault(j => j.MajorVersion == required)
               ?? all.FirstOrDefault(j => j.MajorVersion > required)
               ?? all.FirstOrDefault();
    }

    /// <summary>
    /// 确保数据目录内存在指定主版本的 JRE。
    /// 已存在则直接返回 java.exe 路径；否则从 Adoptium 下载并解压。
    /// 失败返回 null，不会抛给调用方。
    /// </summary>
    public static async Task<string?> EnsureAsync(
        int major,
        string? targetRoot = null,
        CancellationToken ct = default,
        Action<string>? progress = null)
    {
        var root = targetRoot ?? RuntimeRoot;
        var installDir = System.IO.Path.Combine(root, major.ToString());

        // 1) 已经下载过
        var existing = FindJavaExe(installDir);
        if (existing is not null)
        {
            progress?.Invoke($"已就绪：Java {major}");
            return existing;
        }

        // 2) 系统里已有满足条件的，直接复用，避免重复下载
        var system = Detect().FirstOrDefault(j => j.MajorVersion == major);
        if (system is not null)
        {
            progress?.Invoke($"复用系统 Java {major}：{system.Path}");
            return system.Path;
        }

        var zipPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            $"sl-java-{major}-{Guid.NewGuid():N}.zip");

        try
        {
            Directory.CreateDirectory(root);

            progress?.Invoke($"正在下载 Java {major}…");
            var url = $"{AdoptiumRoot}/{major}/ga/windows/x64/jre/hotspot/normal/eclipse";

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SalmonLauncher/" + Boot.Runtime.AppVersion);

                using var response = await http
                    .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength;

                await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var target = File.Create(zipPath);

                var buffer = new byte[81920];
                long read = 0;
                int chunk;
                var lastPercent = -1;

                while ((chunk = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    await target.WriteAsync(buffer.AsMemory(0, chunk), ct).ConfigureAwait(false);
                    read += chunk;

                    if (total is > 0)
                    {
                        var percent = (int)(read * 100 / total.Value);
                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            progress?.Invoke($"下载中 {percent}%");
                        }
                    }
                }
            }

            ct.ThrowIfCancellationRequested();
            progress?.Invoke("解压中…");

            var tempExtract = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"sl-java-x-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempExtract);

            try
            {
                ZipFile.ExtractToDirectory(zipPath, tempExtract);

                // Adoptium 压缩包顶层通常是一个 jdk-xxx-jre 目录，需要把它上提到 installDir
                var sourceRoot = LocateRuntimeRoot(tempExtract);

                if (Directory.Exists(installDir))
                    Directory.Delete(installDir, recursive: true);

                Directory.CreateDirectory(installDir);
                CopyDirectory(sourceRoot, installDir);
            }
            finally
            {
                TryDeleteDirectory(tempExtract);
            }

            var exe = FindJavaExe(installDir);
            if (exe is null)
            {
                AppLog.Warn("Java", $"下载完成但未找到 java.exe：{installDir}");
                progress?.Invoke("下载完成但结构异常");
                return null;
            }

            Invalidate();
            AppLog.Info("Java", $"已下载 Java {major} → {exe}");
            progress?.Invoke($"Java {major} 已就绪");
            return exe;
        }
        catch (OperationCanceledException)
        {
            AppLog.Warn("Java", $"下载 Java {major} 已取消");
            return null;
        }
        catch (Exception ex)
        {
            AppLog.Warn("Java", $"下载 Java {major} 失败：{ex.Message}");
            progress?.Invoke("下载失败，可手动安装 Java");
            return null;
        }
        finally
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

    // ------------------------------------------------------------ 内部实现

    /// <summary>枚举所有可能的 java.exe 候选路径。</summary>
    private static IEnumerable<string> CandidateExecutables()
    {
        // JAVA_HOME
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            var exe = System.IO.Path.Combine(javaHome, "bin", "java.exe");
            if (File.Exists(exe))
                yield return exe;
        }

        // 常见安装根目录
        var roots = new List<string>();
        void AddRoot(string? dir)
        {
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                roots.Add(dir);
        }

        AddRoot(Environment.GetEnvironmentVariable("ProgramFiles"));
        AddRoot(Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
        AddRoot(Environment.GetEnvironmentVariable("LOCALAPPDATA"));

        foreach (var root in roots)
        {
            foreach (var sub in new[] { "Java", "Eclipse Adoptium", "Eclipse Foundation", "Microsoft", "Zulu", "Amazon Corretto", "BellSoft", "Semeru" })
            {
                var dir = System.IO.Path.Combine(root, sub);
                if (!Directory.Exists(dir))
                    continue;

                foreach (var exe in SafeEnumerateJava(dir))
                    yield return exe;
            }
        }

        // 数据目录内已下载的运行时
        foreach (var exe in SafeEnumerateJava(RuntimeRoot))
            yield return exe;

        // PATH 中的 java
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var part in path.Split(System.IO.Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(part))
                continue;

            string? exe = null;
            try
            {
                exe = System.IO.Path.Combine(part.Trim(), "java.exe");
            }
            catch
            {
                // 非法路径段忽略
            }

            if (exe is not null && File.Exists(exe))
                yield return exe;
        }
    }

    private static IEnumerable<string> SafeEnumerateJava(string dir)
    {
        var hits = new List<string>();
        try
        {
            hits.AddRange(Directory.EnumerateFiles(dir, "java.exe", SearchOption.AllDirectories).Take(64));
        }
        catch
        {
            yield break;
        }

        foreach (var hit in hits)
            yield return hit;
    }

    /// <summary>运行 java -version 解析版本；失败返回 null。</summary>
    private static JavaRuntimeInfo? Probe(string exePath)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo(exePath, "-version")
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            var stderr = process.StandardError.ReadToEnd();
            var stdout = process.StandardOutput.ReadToEnd();

            if (!process.WaitForExit(6000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            var text = stderr.Length > 0 ? stderr : stdout;
            var match = VersionRegex.Match(text);
            if (!match.Success)
                return null;

            var first = int.Parse(match.Groups[1].Value);
            var second = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;

            // 1.8.0_392 → 8；17.0.9 → 17；21.0.1 → 21
            var major = first == 1 ? second : first;
            if (major <= 0)
                return null;

            var versionMatch = Regex.Match(text, @"version ""([^""]+)""");
            var version = versionMatch.Success ? versionMatch.Groups[1].Value : major.ToString();

            var isJdk = text.Contains("JDK", StringComparison.OrdinalIgnoreCase)
                        || exePath.Contains("jdk", StringComparison.OrdinalIgnoreCase);

            return new JavaRuntimeInfo(exePath, version, major, isJdk);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>在解压目录里找到真正的运行时根（含 bin/java.exe 的那一层）。</summary>
    private static string LocateRuntimeRoot(string extractDir)
    {
        var direct = System.IO.Path.Combine(extractDir, "bin", "java.exe");
        if (File.Exists(direct))
            return extractDir;

        try
        {
            foreach (var exe in Directory.EnumerateFiles(extractDir, "java.exe", SearchOption.AllDirectories))
            {
                var bin = System.IO.Path.GetDirectoryName(exe);
                if (bin is null)
                    continue;
                var parent = System.IO.Path.GetDirectoryName(bin);
                if (parent is not null)
                    return parent;
            }
        }
        catch
        {
            // 落到下面返回原始目录
        }

        return extractDir;
    }

    private static string? FindJavaExe(string dir)
    {
        if (!Directory.Exists(dir))
            return null;

        var direct = System.IO.Path.Combine(dir, "bin", "java.exe");
        if (File.Exists(direct))
            return direct;

        try
        {
            return Directory.EnumerateFiles(dir, "java.exe", SearchOption.AllDirectories).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var rel = System.IO.Path.GetRelativePath(source, dir);
            Directory.CreateDirectory(System.IO.Path.Combine(destination, rel));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = System.IO.Path.GetRelativePath(source, file);
            var target = System.IO.Path.Combine(destination, rel);
            var parent = System.IO.Path.GetDirectoryName(target);
            if (parent is not null)
                Directory.CreateDirectory(parent);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // 忽略临时目录清理失败
        }
    }
}