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
using System.Text.Json;
using System.Text.RegularExpressions;
using SL.Core.Logging;

namespace SL.Core.Instances;

/// <summary>问题级别。</summary>
public enum ModIssueLevel
{
    /// <summary>缺失依赖等会导致启动失败的问题。</summary>
    Error,

    /// <summary>重复安装、版本不一致等可能出问题的情况。</summary>
    Warning,

    /// <summary>提示性信息，不影响启动。</summary>
    Info,
}

/// <summary>一条依赖检查结果。</summary>
public sealed record ModIssue(ModIssueLevel Level, string ModId, string Message);

/// <summary>一个模组 jar 的元数据。</summary>
public sealed record ModDescriptor(
    string FilePath,
    string ModId,
    string Name,
    string Version,
    string Loader,
    IReadOnlyList<string> DependsOn)
{
    /// <summary>文件名，用于界面显示。</summary>
    public string FileName => Path.GetFileName(FilePath);
}

/// <summary>
/// 模组依赖检查：静态读取 jar 内的元数据，找出缺失依赖与重复安装。
/// 只读文件内容，不加载、不执行任何模组代码。
/// </summary>
public static class ModDependencyChecker
{
    /// <summary>加载器与游戏自身提供的依赖，检查时跳过。</summary>
    private static readonly HashSet<string> BuiltinDeps = new(StringComparer.OrdinalIgnoreCase)
    {
        "minecraft",
        "java",
        "fabricloader",
        "fabric-loader",
        "fabric",
        "fabric-api",
        "quilt_loader",
        "quiltloader",
        "forge",
        "neoforge",
        "javafml",
        "lowcodefml",
        "mixin",
        "mixinextras",
    };

    private static readonly Regex TomlModIdRegex = new(@"^\s*modId\s*=\s*""?([^""\s]+)""?", RegexOptions.Compiled);
    private static readonly Regex TomlDisplayRegex = new(@"^\s*displayName\s*=\s*""?([^""]*)""?", RegexOptions.Compiled);
    private static readonly Regex TomlVersionRegex = new(@"^\s*version\s*=\s*""?([^""\s]+)""?", RegexOptions.Compiled);

    /// <summary>解析单个 jar 的元数据；无法识别时返回 null。</summary>
    public static ModDescriptor? Describe(string jarPath)
    {
        if (string.IsNullOrWhiteSpace(jarPath) || !File.Exists(jarPath))
            return null;

        try
        {
            using var zip = ZipFile.OpenRead(jarPath);

            // Fabric / Quilt 优先：fabric.mod.json
            foreach (var name in new[] { "fabric.mod.json", "quilt.mod.json" })
            {
                var entry = FindEntry(zip, name);
                if (entry is null)
                    continue;

                var parsed = ParseFabricJson(entry, jarPath);
                if (parsed is not null)
                    return parsed;
            }

            // Forge / NeoForge：META-INF/mods.toml 或 META-INF/neoforge.mods.toml
            foreach (var name in new[] { "META-INF/neoforge.mods.toml", "META-INF/mods.toml" })
            {
                var entry = FindEntry(zip, name);
                if (entry is null)
                    continue;

                var loader = name.Contains("neoforge", StringComparison.OrdinalIgnoreCase) ? "neoforge" : "forge";
                var parsed = ParseToml(entry, jarPath, loader);
                if (parsed is not null)
                    return parsed;
            }

            // 旧版 Forge：mcmod.info
            var legacy = FindEntry(zip, "mcmod.info");
            if (legacy is not null)
            {
                var parsed = ParseMcmodInfo(legacy, jarPath);
                if (parsed is not null)
                    return parsed;
            }

            return null;
        }
        catch (Exception ex)
        {
            AppLog.Warn("ModDeps", $"读取 {Path.GetFileName(jarPath)} 失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>扫描目录下全部 jar，返回可识别的模组描述。</summary>
    public static IReadOnlyList<ModDescriptor> Scan(string modsDir)
    {
        var result = new List<ModDescriptor>();

        if (string.IsNullOrWhiteSpace(modsDir) || !Directory.Exists(modsDir))
            return result;

        IEnumerable<string> jars;
        try
        {
            jars = Directory.EnumerateFiles(modsDir, "*.jar", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(modsDir, "*.jar.disabled", SearchOption.TopDirectoryOnly));
        }
        catch (Exception ex)
        {
            AppLog.Warn("ModDeps", "枚举模组目录失败：" + ex.Message);
            return result;
        }

        foreach (var jar in jars)
        {
            var descriptor = Describe(jar);
            if (descriptor is not null)
                result.Add(descriptor);
        }

        return result;
    }

    /// <summary>检查目录下的模组依赖与重复项。</summary>
    public static IReadOnlyList<ModIssue> Check(string modsDir)
    {
        var issues = new List<ModIssue>();

        if (string.IsNullOrWhiteSpace(modsDir) || !Directory.Exists(modsDir))
            return issues;

        List<string> jars;
        try
        {
            jars = Directory.EnumerateFiles(modsDir, "*.jar", SearchOption.TopDirectoryOnly).ToList();
        }
        catch (Exception ex)
        {
            AppLog.Warn("ModDeps", "枚举模组目录失败：" + ex.Message);
            return issues;
        }

        var descriptors = new List<ModDescriptor>();
        foreach (var jar in jars)
        {
            var descriptor = Describe(jar);
            if (descriptor is null)
            {
                issues.Add(new ModIssue(ModIssueLevel.Info, "",
                    $"无法识别 {Path.GetFileName(jar)} 的模组信息，已跳过检查"));
                continue;
            }

            descriptors.Add(descriptor);
        }

        // 重复 modId
        foreach (var group in descriptors.GroupBy(d => d.ModId, StringComparer.OrdinalIgnoreCase))
        {
            var list = group.ToList();
            if (list.Count <= 1)
                continue;

            var files = string.Join(", ", list.Select(d => d.FileName));
            issues.Add(new ModIssue(ModIssueLevel.Warning, group.Key,
                $"模组 {group.Key} 存在 {list.Count} 个版本：{files}，请只保留一个"));
        }

        // 缺失依赖
        var installed = new HashSet<string>(descriptors.Select(d => d.ModId), StringComparer.OrdinalIgnoreCase);
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in descriptors)
        {
            foreach (var dep in descriptor.DependsOn)
            {
                if (BuiltinDeps.Contains(dep))
                    continue;
                if (installed.Contains(dep))
                    continue;

                var key = descriptor.ModId + "->" + dep;
                if (!reported.Add(key))
                    continue;

                issues.Add(new ModIssue(ModIssueLevel.Error, descriptor.ModId,
                    $"模组 {descriptor.Name}（{descriptor.ModId}）依赖 {dep}，但未找到该模组"));
            }
        }

        return issues;
    }

    /// <summary>给界面用的一句话摘要。</summary>
    public static string Summarize(IReadOnlyList<ModIssue> issues)
    {
        if (issues.Count == 0)
            return "模组依赖检查通过";

        var errors = issues.Count(i => i.Level == ModIssueLevel.Error);
        var warnings = issues.Count(i => i.Level == ModIssueLevel.Warning);

        if (errors == 0 && warnings == 0)
            return $"检查完成，{issues.Count} 条提示";

        var parts = new List<string>();
        if (errors > 0)
            parts.Add($"{errors} 个缺失依赖");
        if (warnings > 0)
            parts.Add($"{warnings} 个重复安装");

        return $"{issues.Count} 个问题：" + string.Join("，", parts);
    }

    // ------------------------------------------------------------ 解析实现

    private static ZipArchiveEntry? FindEntry(ZipArchive zip, string name)
    {
        return zip.Entries.FirstOrDefault(e =>
            e.FullName.Replace('\\', '/').Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>解析 fabric.mod.json / quilt.mod.json。</summary>
    private static ModDescriptor? ParseFabricJson(ZipArchiveEntry entry, string jarPath)
    {
        try
        {
            var text = ReadEntryText(entry);
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var loader = entry.Name.Contains("quilt", StringComparison.OrdinalIgnoreCase) ? "quilt" : "fabric";

            var id = GetString(root, "id");
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var name = GetString(root, "name");
            if (string.IsNullOrWhiteSpace(name))
                name = id;

            var version = GetString(root, "version");
            if (string.IsNullOrWhiteSpace(version))
                version = "";

            var depends = new List<string>();
            if (root.TryGetProperty("depends", out var depNode) && depNode.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in depNode.EnumerateObject())
                    depends.Add(prop.Name);
            }

            return new ModDescriptor(jarPath, id!, name!, version!, loader, depends);
        }
        catch (Exception ex)
        {
            AppLog.Warn("ModDeps", $"解析 {Path.GetFileName(jarPath)} 的 fabric.mod.json 失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>解析 mcmod.info（旧版 Forge）。</summary>
    private static ModDescriptor? ParseMcmodInfo(ZipArchiveEntry entry, string jarPath)
    {
        try
        {
            var text = ReadEntryText(entry);
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            JsonElement first;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                if (doc.RootElement.GetArrayLength() == 0)
                    return null;
                first = doc.RootElement[0];
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("modList", out var list)
                    && list.ValueKind == JsonValueKind.Array
                    && list.GetArrayLength() > 0)
                {
                    first = list[0];
                }
                else
                {
                    first = doc.RootElement;
                }
            }
            else
            {
                return null;
            }

            var id = GetString(first, "modid") ?? GetString(first, "modId");
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var name = GetString(first, "name") ?? id;
            var version = GetString(first, "version") ?? "";

            return new ModDescriptor(jarPath, id!, name!, version!, "forge", Array.Empty<string>());
        }
        catch (Exception ex)
        {
            AppLog.Warn("ModDeps", $"解析 {Path.GetFileName(jarPath)} 的 mcmod.info 失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>逐行解析 mods.toml，避免引入额外 TOML 依赖。</summary>
    private static ModDescriptor? ParseToml(ZipArchiveEntry entry, string jarPath, string loader)
    {
        try
        {
            var text = ReadEntryText(entry);
            var lines = text.Replace("\r\n", "\n").Split('\n');

            string? firstModId = null;
            string? firstDisplayName = null;
            string? firstVersion = null;
            var depends = new List<string>();

            var inModsSection = false;
            var inDependencies = false;

            foreach (var raw in lines)
            {
                var line = raw.Trim();

                if (line.StartsWith("[[", StringComparison.Ordinal))
                {
                    inModsSection = line.StartsWith("[[mods]]", StringComparison.OrdinalIgnoreCase);
                    inDependencies = line.StartsWith("[[dependencies.", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    inModsSection = false;
                    inDependencies = false;
                    continue;
                }

                if (inModsSection)
                {
                    var idMatch = TomlModIdRegex.Match(line);
                    if (idMatch.Success && firstModId is null)
                        firstModId = idMatch.Groups[1].Value;

                    var nameMatch = TomlDisplayRegex.Match(line);
                    if (nameMatch.Success && firstDisplayName is null)
                        firstDisplayName = nameMatch.Groups[1].Value;

                    var versionMatch = TomlVersionRegex.Match(line);
                    if (versionMatch.Success && firstVersion is null)
                        firstVersion = versionMatch.Groups[1].Value;
                }
                else if (inDependencies)
                {
                    var depMatch = TomlModIdRegex.Match(line);
                    if (depMatch.Success && !depMatch.Groups[1].Value.Contains('$'))
                        depends.Add(depMatch.Groups[1].Value);
                }
            }

            if (string.IsNullOrWhiteSpace(firstModId))
                return null;

            return new ModDescriptor(
                jarPath,
                firstModId!,
                string.IsNullOrWhiteSpace(firstDisplayName) ? firstModId! : firstDisplayName!,
                firstVersion ?? "",
                loader,
                depends);
        }
        catch (Exception ex)
        {
            AppLog.Warn("ModDeps", $"解析 {Path.GetFileName(jarPath)} 的 mods.toml 失败：{ex.Message}");
            return null;
        }
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (!element.TryGetProperty(property, out var node))
            return null;

        return node.ValueKind switch
        {
            JsonValueKind.String => node.GetString(),
            JsonValueKind.Number => node.GetRawText(),
            _ => null,
        };
    }
}