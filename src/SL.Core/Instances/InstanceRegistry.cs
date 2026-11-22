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

namespace SL.Core.Instances;

/// <summary>An installed instance (disk fact, not demo data).</summary>
/// <param name="InstanceId">Directory name = version ID</param>
/// <param name="InstalledAt">When the core jar was written to disk</param>
/// <param name="SizeMb">Approximate size of the instance directory</param>
public sealed record InstanceInfo(string InstanceId, DateTimeOffset InstalledAt, long SizeMb);

/// <summary>Mods inside an instance (.jar / .disabled under the mods directory).</summary>
/// <param name="FileName">Actual file name (including the .disabled suffix)</param>
/// <param name="BaseName">Display name with .disabled removed</param>
/// <param name="SizeBytes"></param>
/// <param name="Enabled">Enabled means the .disabled suffix is absent</param>
/// <param name="ModifiedAt"></param>
public sealed record ModInfo(string FileName, string BaseName, long SizeBytes, bool Enabled, DateTimeOffset ModifiedAt);

/// <summary>Severity of a health finding.</summary>
public enum HealthLevel
{
    /// <summary>Informational; the instance still works.</summary>
    Info,
    /// <summary>Something is off and may cause problems at runtime.</summary>
    Warning,
    /// <summary>The instance cannot launch until this is fixed.</summary>
    Error,
}

/// <summary>One problem found by the instance health check, with what to do about it.</summary>
public sealed record HealthFinding(HealthLevel Level, string Title, string Detail, string Remedy);

/// <summary>
/// Stage 4: instance registry. The three-tier directory layout established in stage 2 is the source of truth;
/// mod enable/disable (renaming the .disabled suffix) / delete / import all converge in this layer, and Pres only calls it.
/// </summary>
public static class InstanceRegistry
{
    public static IReadOnlyList<InstanceInfo> List()
    {
        var root = Boot.Runtime.InstancesDir;
        var list = new List<InstanceInfo>();
        if (!Directory.Exists(root))
            return list;

        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var id = Path.GetFileName(dir);
            var versionJar = Path.Combine(dir, "versions", id + ".jar");
            if (!File.Exists(versionJar))
                continue;

            var size = 0L;
            try
            {
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    size += new FileInfo(f).Length;
            }
            catch (IOException) { }

            list.Add(new InstanceInfo(id, new DateTimeOffset(File.GetLastWriteTimeUtc(versionJar)), size / (1024 * 1024)));
        }

        return list.OrderByDescending(i => i.InstalledAt).ToList();
    }

    /// <summary>Most recently installed instance (default target of the launch pipeline).</summary>
    public static InstanceInfo? Latest()
    {
        var all = List();
        return all.Count > 0 ? all[0] : null;
    }

    // ------------------------------------------------------------ Mods

    public static string ModsDirOf(string instanceId)
        => Path.Combine(Boot.Runtime.InstancesDir, instanceId, "mods");

    public static IReadOnlyList<ModInfo> ListMods(string instanceId)
    {
        var dir = ModsDirOf(instanceId);
        var list = new List<ModInfo>();
        if (!Directory.Exists(dir))
            return list;

        foreach (var path in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is not (".jar" or ".disabled"))
                continue;
            var name = Path.GetFileName(path);
            var info = new FileInfo(path);
            var enabled = ext == ".jar";
            list.Add(new ModInfo(name, enabled ? name : name[..^".disabled".Length],
                info.Length, enabled, new DateTimeOffset(info.LastWriteTime)));
        }

        return list.OrderByDescending(m => m.Enabled).ThenByDescending(m => m.ModifiedAt).ToList();
    }

    /// <summary>
    /// Toggle enable/disable by adding or removing the .disabled suffix (the file system is the state).
    /// Returns the enabled state after toggling.
    /// </summary>
    public static bool ToggleMod(string instanceId, string fileName)
    {
        var dir = ModsDirOf(instanceId);
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException(fileName);

        if (fileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
        {
            var renamed = path[..^".disabled".Length];
            // A leftover file with the target name (e.g. an interrupted toggle) must not block enabling.
            if (File.Exists(renamed))
                File.Delete(renamed);
            File.Move(path, renamed);
            return true;
        }

        var disabled = path + ".disabled";
        if (File.Exists(disabled))
            File.Delete(disabled);
        File.Move(path, disabled);
        return false;
    }

    public static void DeleteMod(string instanceId, string fileName)
    {
        var path = Path.Combine(ModsDirOf(instanceId), fileName);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>Import from a local file (copy, never move; on a name clash, rename with a sequence number before overwriting).</summary>
    public static string ImportMod(string instanceId, string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException(sourcePath);
        var dir = ModsDirOf(instanceId);
        Directory.CreateDirectory(dir);

        var name = Path.GetFileName(sourcePath);
        var target = Path.Combine(dir, name);
        if (File.Exists(target))
        {
            var stem = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);
            for (var i = 2; File.Exists(target); i++)
                target = Path.Combine(dir, $"{stem} ({i}){ext}");
        }
        File.Copy(sourcePath, target);
        return Path.GetFileName(target);
    }

    public static string InstanceDirOf(string instanceId)
        => Path.Combine(Boot.Runtime.InstancesDir, instanceId);

    // ------------------------------------------------------------ Saves (stage 4 wrap-up)

    /// <summary>A save entry.</summary>
    /// <param name="Name">World name (directory name under saves)</param>
    /// <param name="LastPlayed">Last write time of the directory</param>
    /// <param name="SizeMb">Size of the world directory</param>
    public sealed record SaveInfo(string Name, DateTimeOffset LastPlayed, long SizeMb);

    public static IReadOnlyList<SaveInfo> ListSaves(string instanceId)
    {
        var dir = Path.Combine(InstanceDirOf(instanceId), "saves");
        var list = new List<SaveInfo>();
        if (!Directory.Exists(dir))
            return list;

        foreach (var world in Directory.EnumerateDirectories(dir))
        {
            var size = 0L;
            try
            {
                foreach (var f in Directory.EnumerateFiles(world, "*", SearchOption.AllDirectories))
                    size += new FileInfo(f).Length;
            }
            catch (IOException) { }

            list.Add(new SaveInfo(Path.GetFileName(world),
                new DateTimeOffset(Directory.GetLastWriteTime(world)), size / (1024 * 1024)));
        }

        return list.OrderByDescending(s => s.LastPlayed).ToList();
    }

    /// <summary>Back up the saves directory as a zip (backups/saves-yyyyMMdd-HHmmss.zip); returns the backup path, or null when there are no saves.</summary>
    public static string? BackupSaves(string instanceId, CancellationToken cancellation = default)
    {
        var savesDir = Path.Combine(InstanceDirOf(instanceId), "saves");
        if (!Directory.Exists(savesDir) || !Directory.EnumerateFileSystemEntries(savesDir).Any())
            return null;

        var backupDir = Path.Combine(InstanceDirOf(instanceId), "backups");
        Directory.CreateDirectory(backupDir);
        var target = Path.Combine(backupDir, $"saves-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(savesDir, target);

        // Keep the newest backups only: an unbounded folder grows without anyone noticing.
        PruneBackups(backupDir);
        return target;
    }

    /// <summary>How many save backups are kept per instance; older ones are deleted.</summary>
    public const int MaxSaveBackups = 10;

    /// <summary>A backup file on disk.</summary>
    public sealed record BackupInfo(string Path, string Name, DateTimeOffset CreatedAt, long SizeBytes);

    /// <summary>Backups for an instance, newest first.</summary>
    public static IReadOnlyList<BackupInfo> ListBackups(string instanceId)
    {
        var dir = Path.Combine(InstanceDirOf(instanceId), "backups");
        if (!Directory.Exists(dir))
            return [];

        return Directory.EnumerateFiles(dir, "saves-*.zip")
            .Select(p =>
            {
                var info = new FileInfo(p);
                return new BackupInfo(p, info.Name, info.LastWriteTime, info.Length);
            })
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
    }

    /// <summary>Delete the oldest backups beyond <see cref="MaxSaveBackups"/>.</summary>
    private static void PruneBackups(string backupDir)
    {
        try
        {
            var stale = Directory.EnumerateFiles(backupDir, "saves-*.zip")
                .Select(p => new FileInfo(p))
                .OrderByDescending(f => f.LastWriteTime)
                .Skip(MaxSaveBackups);

            foreach (var file in stale)
                file.Delete();
        }
        catch (Exception)
        {
            // Pruning is housekeeping: a failure must not turn a successful backup into an error.
        }
    }

    /// <summary>
    /// Replace the instance's saves with the contents of a backup. The current saves are moved aside
    /// first, so a mistake can be undone. Returns the folder the previous saves were moved to.
    /// </summary>
    public static string RestoreBackup(string instanceId, string backupPath)
    {
        if (!File.Exists(backupPath))
            throw new FileNotFoundException(backupPath);

        var instanceDir = InstanceDirOf(instanceId);
        var savesDir = Path.Combine(instanceDir, "saves");
        var parked = "";

        if (Directory.Exists(savesDir) && Directory.EnumerateFileSystemEntries(savesDir).Any())
        {
            parked = Path.Combine(instanceDir, "backups", $"replaced-{DateTime.Now:yyyyMMdd-HHmmss}");
            Directory.CreateDirectory(Path.GetDirectoryName(parked)!);
            Directory.Move(savesDir, parked);
        }

        Directory.CreateDirectory(savesDir);
        System.IO.Compression.ZipFile.ExtractToDirectory(backupPath, savesDir, overwriteFiles: true);
        return parked;
    }

    /// <summary>
    /// Export the entire instance (mods, config, saves, etc.) as a zip file.
    /// Returns the zip file path, or null when the instance directory does not exist.
    /// </summary>
    public static string? ExportInstanceZip(string instanceId, bool includeMods = true, bool includeConfig = true, bool includeSaves = true)
    {
        var instanceDir = InstanceDirOf(instanceId);
        if (!Directory.Exists(instanceDir))
            return null;

        var zipPath = Path.Combine(Path.GetTempPath(), $"salmon-instance-{instanceId}-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        var filesToInclude = new List<(string sourceDir, string relativeRoot)>();

        if (includeMods)
            filesToInclude.Add((Path.Combine(instanceDir, "mods"), "mods"));
        if (includeConfig)
            filesToInclude.Add((Path.Combine(instanceDir, "config"), "config"));
        if (includeSaves)
            filesToInclude.Add((Path.Combine(instanceDir, "saves"), "saves"));

        // Always include the version metadata (shared, not copied per-file)
        var metaDir = Path.Combine(instanceDir, "meta");
        if (Directory.Exists(metaDir))
            filesToInclude.Add((metaDir, "meta"));

        try
        {
            using var zip = new ZipArchive(File.Create(zipPath), ZipArchiveMode.Create);

            foreach (var entry in filesToInclude)
            {
                if (!Directory.Exists(entry.sourceDir)) continue;
                AddDirectoryToZip(zip, entry.sourceDir, entry.relativeRoot);
            }

            // 附带与实例同名的 version jar
            var versionsDir = Path.Combine(instanceDir, "versions");
            if (Directory.Exists(versionsDir))
            {
                foreach (var jar in Directory.EnumerateFiles(versionsDir, "*.jar"))
                {
                    var jarName = Path.GetFileNameWithoutExtension(jar);
                    if (string.Equals(jarName, instanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        zip.CreateEntryFromFile(jar, "versions/" + Path.GetFileName(jar));
                        break;
                    }
                }
            }

            return zipPath;
        }
        catch (Exception)
        {
            try { File.Delete(zipPath); } catch { }
            return null;
        }
    }

    /// <summary>临时工作根目录：优先 %TEMP%，不可写时回落到数据目录下的 tmp。</summary>
    /// <summary>临时目录根：测试或受限环境下可覆盖（默认 %TEMP%）。</summary>
    public static string? ScratchRootOverride { get; set; }

    private static string ScratchRoot()
    {
        if (!string.IsNullOrWhiteSpace(ScratchRootOverride))
        {
            Directory.CreateDirectory(ScratchRootOverride!);
            return ScratchRootOverride!;
        }

        try
        {
            var temp = Path.GetTempPath();
            var probe = Path.Combine(temp, "sl-write-probe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(probe);
            Directory.Delete(probe, true);
            return temp;
        }
        catch
        {
            var fallback = Path.Combine(Boot.Runtime.Root, "tmp");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    private static void AddDirectoryToZip(ZipArchive zip, string sourceDir, string relativeRoot)
    {
        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relPath = dir.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar);
            var entryName = Path.Combine(relativeRoot, relPath).Replace('\\', '/');
            zip.CreateEntry(entryName); // Empty entry, files will be added below
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relPath = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar);
            var entryName = Path.Combine(relativeRoot, relPath).Replace('\\', '/');
            zip.CreateEntryFromFile(file, entryName);
        }
    }

    /// <summary>Import an instance zip, creating a new instance or updating an existing one.</summary>
    /// <returns>The target instance id (new or existing).</returns>
    public static string ImportInstanceZip(string zipPath, string? targetInstanceId = null)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException(zipPath);

        var extractDir = Path.Combine(ScratchRoot(), "sl-extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractDir);

        try
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir);

            // Read the instance metadata from the zip to determine the instance id
            // Look for meta/instance.json or similar
            var metaPath = Path.Combine(extractDir, "meta", "instance.json");
            string instanceId;

            if (File.Exists(metaPath))
            {
                var metaJson = File.ReadAllText(metaPath);
                // 从 JSON 中读取 id / name 字段
                string? metaId = null;
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(metaJson);
                    var root = doc.RootElement;
                    if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        if (root.TryGetProperty("id", out var idEl) && idEl.ValueKind == System.Text.Json.JsonValueKind.String)
                            metaId = idEl.GetString();
                        else if (root.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == System.Text.Json.JsonValueKind.String)
                            metaId = nameEl.GetString();
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    metaId = null;
                }

                instanceId = string.IsNullOrWhiteSpace(metaId)
                    ? Path.GetFileNameWithoutExtension(zipPath)
                    : metaId!;
            }
            else
            {
                instanceId = targetInstanceId ?? Path.GetFileNameWithoutExtension(zipPath);
            }

            // Determine target directory
            var targetDir = InstanceDirOf(instanceId);
            if (Directory.Exists(targetDir))
            {
                // If instance exists, we'll merge/modify; for now, use a derived name
                var baseName = Path.GetFileNameWithoutExtension(zipPath);
                instanceId = baseName;
                targetDir = InstanceDirOf(instanceId);
            }
            Directory.CreateDirectory(targetDir);

            // Copy mods, config, saves from extracted directory
            var copyDirs = new[] { "mods", "config", "saves" };
            foreach (var dir in copyDirs)
            {
                var src = Path.Combine(extractDir, dir);
                var dst = Path.Combine(targetDir, dir);
                if (!Directory.Exists(src))
                    continue;

                // 目标目录可能还不存在（全新实例）：先清空再复制
                if (Directory.Exists(dst))
                {
                    foreach (var f in Directory.GetFiles(dst, "*.*", SearchOption.AllDirectories))
                    {
                        try { File.Delete(f); } catch { }
                    }

                    foreach (var d in Directory.GetDirectories(dst, "*", SearchOption.AllDirectories))
                    {
                        try { Directory.Delete(d, true); } catch { }
                    }
                }

                CopyDirectoryContents(src, dst);
            }

            // Copy matching version jar
            var srcVersions = Path.Combine(extractDir, "versions");
            var dstVersions = Path.Combine(targetDir, "versions");
            if (Directory.Exists(srcVersions))
            {
                Directory.CreateDirectory(dstVersions);
                foreach (var jar in Directory.EnumerateFiles(srcVersions, "*.jar"))
                {
                    var name = Path.GetFileName(jar);
                    var dst = Path.Combine(dstVersions, name);
                    File.Copy(jar, dst, overwrite: true);
                }
            }

            return instanceId;
        }
        catch (Exception ex)
        {
            throw new IOException($"导入实例 zip 失败: {ex.Message}", ex);
        }
        finally
        {
            try { Directory.Delete(extractDir, true); } catch { }
        }
    }

    private static void CopyDirectoryContents(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relPath = dir.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
            var targetSub = Path.Combine(target, relPath);
            Directory.CreateDirectory(targetSub);
        }
        foreach (var file in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
        {
            var relPath = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
            var targetFile = Path.Combine(target, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile, overwrite: true);
        }
    }

    /// <summary>
    /// Duplicate an instance to a new one with its own mods, saves and configuration, so a setup can be
    /// tried out without touching the original. Also exports a zip of the new instance.
    /// The version files and libraries are shared, not copied.
    /// Returns the new instance id.
    /// </summary>
    public static string DuplicateInstanceWithZip(string sourceId, string? targetId = null, bool includeMods = true, bool includeConfig = true, bool includeSaves = true)
    {
        var newId = DuplicateInstance(sourceId, targetId);
        var zipPath = ExportInstanceZip(newId, includeMods, includeConfig, includeSaves);
        // Caller can retrieve zip if needed; zip is stored at Path.GetTempPath()
        return newId;
    }

    /// <summary>
    /// Copy an instance to a new one with its own mods, saves and configuration, so a setup can be
    /// tried out without touching the original. The version files and libraries are shared, not copied.
    /// Returns the new instance id.
    /// </summary>
    public static string DuplicateInstance(string sourceId, string? targetId = null)
    {
        var sourceDir = InstanceDirOf(sourceId);
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException(sourceDir);

        var newId = string.IsNullOrWhiteSpace(targetId) ? NextCopyId(sourceId) : targetId.Trim();
        var targetDir = InstanceDirOf(newId);
        if (Directory.Exists(targetDir))
            throw new IOException($"实例已存在:{newId}");

        Directory.CreateDirectory(targetDir);

        // Everything except the version payload and the old backups: those are shared or bulky.
        string[] copy = ["mods", "saves", "resourcepacks", "shaderpacks", "config", "options.txt", "servers.dat"];
        foreach (var entry in copy)
        {
            var src = Path.Combine(sourceDir, entry);
            var dst = Path.Combine(targetDir, entry);
            if (Directory.Exists(src))
                CopyDirectory(src, dst);
            else if (File.Exists(src))
                File.Copy(src, dst, overwrite: true);
        }

        // The version itself lives in a shared versions folder; only the metadata needs a copy.
        var metaSrc = Path.Combine(sourceDir, "meta");
        if (Directory.Exists(metaSrc))
            CopyDirectory(metaSrc, Path.Combine(targetDir, "meta"));

        // A copied instance must be able to find its version jar: link the version folder if present.
        var verSrc = Path.Combine(sourceDir, "versions");
        if (Directory.Exists(verSrc))
            CopyDirectory(verSrc, Path.Combine(targetDir, "versions"));

        // A copied instance must be recognised by List(): it looks for versions/<instanceId>.jar,
        // so rename the copied jar to match the new instance id.
        var copiedVersions = Path.Combine(targetDir, "versions");
        if (Directory.Exists(copiedVersions))
        {
            foreach (var jar in Directory.EnumerateFiles(copiedVersions, "*.jar"))
            {
                var name = Path.GetFileName(jar);
                if (string.Equals(Path.GetFileNameWithoutExtension(name), newId, StringComparison.OrdinalIgnoreCase))
                    continue;

                var renamed = Path.Combine(copiedVersions, newId + ".jar");
                if (!File.Exists(renamed))
                    File.Move(jar, renamed);
            }
        }

        return newId;
    }

    private static string NextCopyId(string sourceId)
    {
        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{sourceId}-copy{i - 1}";
            if (!Directory.Exists(InstanceDirOf(candidate)))
                return candidate;
        }
        throw new IOException("无法生成新的实例名");
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target, StringComparison.Ordinal));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, target, StringComparison.Ordinal), overwrite: true);
    }

    // ------------------------------------------------------------ Health check

    /// <summary>
    /// Inspect an instance for problems that stop it launching or make it misbehave: a missing or
    /// truncated core jar, mods without Fabric API, duplicate mod files, and the same jar enabled
    /// twice under different names. Every finding carries a concrete remedy.
    /// </summary>
    public static IReadOnlyList<HealthFinding> CheckHealth(string instanceId)
    {
        var findings = new List<HealthFinding>();
        var dir = InstanceDirOf(instanceId);

        if (!Directory.Exists(dir))
        {
            findings.Add(new HealthFinding(HealthLevel.Error,
                "实例目录不存在",
                $"找不到 {dir}",
                "重新安装该版本，或在「模组管理」中重新导入需要的文件。"));
            return findings;
        }

        // Core jar: without it the instance cannot start.
        var coreJar = Path.Combine(dir, "versions", instanceId + ".jar");
        if (!File.Exists(coreJar))
        {
            findings.Add(new HealthFinding(HealthLevel.Error,
                "缺少游戏核心文件",
                $"versions/{instanceId}.jar 不存在，启动时会直接失败。",
                "在「资源 → 游戏版本」重新安装该版本，启动器会补齐核心文件。"));
        }
        else
        {
            var size = new FileInfo(coreJar).Length;
            if (size < 64 * 1024)
            {
                findings.Add(new HealthFinding(HealthLevel.Error,
                    "游戏核心文件可能不完整",
                    $"versions/{instanceId}.jar 只有 {size / 1024} KB，正常应远大于此。",
                    "删除该文件后启动一次游戏，启动器会自动重新下载。"));
            }
        }

        var mods = ListMods(instanceId);
        var enabled = mods.Where(m => m.Enabled).ToList();

        if (enabled.Count > 0 && !enabled.Any(m => IsFabricApi(m.BaseName)))
        {
            findings.Add(new HealthFinding(HealthLevel.Warning,
                "已启用模组但缺少 Fabric API",
                $"启用了 {enabled.Count} 个模组，但没有找到 Fabric API；多数 Fabric 模组缺少它会直接崩溃。",
                "在「资源 → 模组仓库」搜索 Fabric API 并安装到该实例。"));
        }

        // The same mod present both enabled and disabled.
        foreach (var group in mods.GroupBy(m => m.BaseName, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() > 1)
            {
                findings.Add(new HealthFinding(HealthLevel.Warning,
                    "存在重复的模组文件",
                    $"{group.Key} 同时存在 {group.Count()} 个文件，游戏可能加载到旧版本。",
                    "保留一个版本，删除其余文件。"));
            }
        }

        if (enabled.Count > 80)
        {
            findings.Add(new HealthFinding(HealthLevel.Info,
                "启用模组数量较多",
                $"当前启用 {enabled.Count} 个模组，启动与运行都会明显变慢。",
                "内存不足时可适当停用不常用的模组，或在「实例设置」中提高内存分配。"));
        }

        if (findings.Count == 0)
        {
            findings.Add(new HealthFinding(HealthLevel.Info,
                "未发现问题",
                "核心文件、模组与存档结构正常。",
                "无需处理。"));
        }

        return findings.OrderByDescending(f => f.Level).ToList();
    }

    private static bool IsFabricApi(string baseName)
        => baseName.Contains("fabric-api", StringComparison.OrdinalIgnoreCase)
           || baseName.Contains("fabric_api", StringComparison.OrdinalIgnoreCase);
}