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
using System.Security.Cryptography;
using System.Text.Json;

namespace SL.Core.Instances;

/// <summary>Which packaging a modpack zip uses.</summary>
public enum ModpackFormat
{
    /// <summary>Modrinth .mrpack (modrinth.index.json with download URLs).</summary>
    Mrpack,
    /// <summary>CurseForge manifest.json (project/file ids only, no direct URLs).</summary>
    CurseForge,
    /// <summary>A plain zip that just contains mods/ and friends.</summary>
    Plain,
}

/// <summary>One file a modpack wants on disk.</summary>
/// <param name="Path">Instance-relative destination, e.g. mods/sodium.jar</param>
/// <param name="Url">Direct download URL when the format provides one</param>
/// <param name="Sha1">Expected SHA-1 (lowercase hex) when known</param>
/// <param name="Size">Expected size in bytes, -1 when unknown</param>
/// <param name="FromZip">Entry name inside the zip when the file ships with the pack</param>
public sealed record ModpackFile(string Path, string? Url, string? Sha1, long Size, string? FromZip);

/// <summary>What a modpack says it needs, read without extracting anything.</summary>
public sealed record ModpackManifest(
    ModpackFormat Format,
    string Name,
    string Version,
    string GameVersion,
    string Loader,
    string LoaderVersion,
    IReadOnlyList<ModpackFile> Files,
    IReadOnlyList<string> Notes)
{
    /// <summary>Number of mod jars the pack will place under mods/.</summary>
    public int ModCount => Files.Count(f => f.Path.StartsWith("mods/", StringComparison.OrdinalIgnoreCase));

    /// <summary>True when the pack needs a mod loader (i.e. is not pure vanilla).</summary>
    public bool NeedsLoader => Loader.Length > 0 && !Loader.Equals("原版", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Outcome of an import.</summary>
public sealed record ModpackImportResult(
    string InstanceId,
    int Imported,
    int Downloaded,
    int Skipped,
    int Failed,
    IReadOnlyList<string> Failures);

/// <summary>
/// Zip modpack import. Supports the three layouts that actually circulate:
/// <list type="bullet">
/// <item>Modrinth .mrpack — has direct download URLs, so it can be installed fully offline-free of any API key.</item>
/// <item>CurseForge export — carries only project/file ids; those need the CurseForge API, so only the
/// bundled <c>overrides/</c> (configs, saves, resource packs) can be placed, and the caller is told so.</item>
/// <item>Plain zip — anything that ships a <c>mods/</c> folder or loose jars; copied as-is.</item>
/// </list>
/// </summary>
public static class ModpackImporter
{
    private static readonly HttpClient Http = CreateHttp();
    private static readonly JsonDocumentOptions JsonOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    private static HttpClient CreateHttp()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SalmonLauncher/" + Boot.Runtime.AppVersion);
        return client;
    }

    // ------------------------------------------------------------ inspect

    /// <summary>Read the pack manifest without touching the disk (beyond opening the zip).</summary>
    public static ModpackManifest Inspect(string zipPath)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException(zipPath);

        using var zip = ZipFile.OpenRead(zipPath);
        var names = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();

        var mrpack = Find(zip, "modrinth.index.json");
        if (mrpack is not null)
            return ReadMrpack(mrpack, names);

        var curse = Find(zip, "manifest.json");
        if (curse is not null)
            return ReadCurseForge(curse, names);

        return ReadPlain(zip, names);
    }

    private static ZipArchiveEntry? Find(ZipArchive zip, string fileName)
        => zip.Entries.FirstOrDefault(e =>
        {
            var full = e.FullName.Replace('\\', '/');
            return full.Equals(fileName, StringComparison.OrdinalIgnoreCase)
                   || full.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase);
        });

    private static ModpackManifest ReadMrpack(ZipArchiveEntry entry, IReadOnlyList<string> names)
    {
        using var stream = entry.Open();
        using var doc = JsonDocument.Parse(stream, JsonOptions);
        var root = doc.RootElement;

        var name = Str(root, "name") ?? "未命名整合包";
        var version = Str(root, "versionId") ?? "";

        var game = "";
        var loader = "";
        var loaderVersion = "";
        if (root.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Object)
        {
            foreach (var dep in deps.EnumerateObject())
            {
                var key = dep.Name.ToLowerInvariant();
                var value = dep.Value.GetString() ?? "";
                switch (key)
                {
                    case "minecraft":
                        game = value;
                        break;
                    case "fabric-loader":
                        loader = "Fabric";
                        loaderVersion = value;
                        break;
                    case "quilt-loader":
                        loader = "Quilt";
                        loaderVersion = value;
                        break;
                    case "forge":
                        loader = "Forge";
                        loaderVersion = value;
                        break;
                    case "neoforge":
                        loader = "NeoForge";
                        loaderVersion = value;
                        break;
                }
            }
        }

        var files = new List<ModpackFile>();
        if (root.TryGetProperty("files", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                var path = Str(item, "path");
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                // honour the pack's own client/server split (env.client == "unsupported")
                if (item.TryGetProperty("env", out var env) && env.ValueKind == JsonValueKind.Object
                    && Str(env, "client") is { } clientSide
                    && clientSide.Equals("unsupported", StringComparison.OrdinalIgnoreCase))
                    continue;

                var url = "";
                if (item.TryGetProperty("downloads", out var downloads) && downloads.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in downloads.EnumerateArray())
                    {
                        var candidate = d.GetString();
                        if (!string.IsNullOrWhiteSpace(candidate))
                        {
                            url = candidate;
                            break;
                        }
                    }
                }

                var sha1 = "";
                if (item.TryGetProperty("hashes", out var hashes) && hashes.ValueKind == JsonValueKind.Object)
                    sha1 = Str(hashes, "sha1") ?? "";

                var size = item.TryGetProperty("fileSize", out var sizeNode) && sizeNode.TryGetInt64(out var parsed)
                    ? parsed
                    : -1;

                files.Add(new ModpackFile(Normalize(path), url.Length > 0 ? url : null,
                    sha1.Length > 0 ? sha1 : null, size, null));
            }
        }

        // overrides/ ships with the pack and must be placed verbatim
        foreach (var entryName in names)
        {
            var relative = OverrideRelative(entryName, "overrides/");
            if (relative is null)
                continue;
            if (files.Any(f => f.Path.Equals(relative, StringComparison.OrdinalIgnoreCase)))
                continue;
            files.Add(new ModpackFile(relative, null, null, -1, entryName));
        }

        var notes = new List<string>();
        if (game.Length == 0)
            notes.Add("整合包未声明 Minecraft 版本，导入后请确认实例版本。");
        if (loader.Length == 0)
            notes.Add("整合包未声明加载器（纯原版整合包）。");

        return new ModpackManifest(ModpackFormat.Mrpack, name, version, game, loader, loaderVersion, files, notes);
    }

    private static ModpackManifest ReadCurseForge(ZipArchiveEntry entry, IReadOnlyList<string> names)
    {
        using var stream = entry.Open();
        using var doc = JsonDocument.Parse(stream, JsonOptions);
        var root = doc.RootElement;

        var name = Str(root, "name") ?? "未命名整合包";
        var version = Str(root, "version") ?? "";

        var game = "";
        var loader = "";
        var loaderVersion = "";
        if (root.TryGetProperty("minecraft", out var mc) && mc.ValueKind == JsonValueKind.Object)
        {
            game = Str(mc, "version") ?? "";
            if (mc.TryGetProperty("modLoaders", out var loaders) && loaders.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in loaders.EnumerateArray())
                {
                    var primary = item.TryGetProperty("primary", out var p) && p.ValueKind == JsonValueKind.True;
                    var id = Str(item, "id") ?? "";
                    if (id.Length == 0)
                        continue;
                    if (loader.Length > 0 && !primary)
                        continue;
                    var dash = id.IndexOf('-');
                    var kind = (dash > 0 ? id[..dash] : id).ToLowerInvariant();
                    loader = kind switch
                    {
                        "forge" => "Forge",
                        "neoforge" => "NeoForge",
                        "fabric" => "Fabric",
                        "quilt" => "Quilt",
                        _ => loader,
                    };
                    loaderVersion = dash > 0 ? id[(dash + 1)..] : "";
                    if (primary)
                        break;
                }
            }
        }

        var files = new List<ModpackFile>();
        var overrides = Str(root, "overrides") ?? "overrides";
        var overridesPrefix = overrides.TrimEnd('/') + "/";
        foreach (var entryName in names)
        {
            var relative = OverrideRelative(entryName, overridesPrefix);
            if (relative is null)
                continue;
            files.Add(new ModpackFile(relative, null, null, -1, entryName));
        }

        var notes = new List<string>
        {
            "CurseForge 整合包只提供 projectID/fileID，下载模组需要 CurseForge 官方 API Key，本启动器暂不内置。",
            "已导入整合包自带文件（config、saves、resourcepacks 等）；模组本体请到「资源 → 模组仓库」按名称搜索安装。",
        };

        return new ModpackManifest(ModpackFormat.CurseForge, name, version, game, loader, loaderVersion, files, notes);
    }

    private static ModpackManifest ReadPlain(ZipArchive zip, IReadOnlyList<string> names)
    {
        var files = new List<ModpackFile>();
        foreach (var entryName in names)
        {
            var normalized = Normalize(entryName);
            if (normalized.EndsWith('/'))
                continue;

            // strip a leading .minecraft/ so both layouts land in the same place
            var relative = normalized.StartsWith(".minecraft/", StringComparison.OrdinalIgnoreCase)
                ? normalized[".minecraft/".Length..]
                : normalized;

            var ext = Path.GetExtension(relative).ToLowerInvariant();
            var inMods = relative.StartsWith("mods/", StringComparison.OrdinalIgnoreCase);
            var looseJar = !relative.Contains('/') && ext == ".jar";
            var managed = relative.StartsWith("config/", StringComparison.OrdinalIgnoreCase)
                          || relative.StartsWith("saves/", StringComparison.OrdinalIgnoreCase)
                          || relative.StartsWith("resourcepacks/", StringComparison.OrdinalIgnoreCase)
                          || relative.StartsWith("shaderpacks/", StringComparison.OrdinalIgnoreCase)
                          || relative.StartsWith("defaultconfigs/", StringComparison.OrdinalIgnoreCase);

            if (!inMods && !looseJar && !managed)
                continue;

            var destination = looseJar ? "mods/" + relative : relative;
            files.Add(new ModpackFile(destination, null, null, -1, entryName));
        }

        var notes = new List<string>();
        if (files.Count == 0)
            notes.Add("压缩包里没有找到 mods/ 目录或 .jar 文件，可能不是整合包。");

        return new ModpackManifest(ModpackFormat.Plain, "本地整合包", "", "", "", "", files, notes);
    }

    // ------------------------------------------------------------ import

    /// <summary>
    /// Place every file of the pack into the instance: files with a URL are downloaded, files shipped in
    /// the zip are extracted, and existing destinations are kept unless their content differs.
    /// </summary>
    public static async Task<ModpackImportResult> ImportAsync(
        string zipPath, string instanceId, Action<string>? step, CancellationToken ct)
    {
        var manifest = Inspect(zipPath);
        var instanceDir = InstanceRegistry.InstanceDirOf(instanceId);
        Directory.CreateDirectory(instanceDir);

        using var zip = ZipFile.OpenRead(zipPath);
        var byName = zip.Entries.ToDictionary(e => e.FullName.Replace('\\', '/'), e => e, StringComparer.OrdinalIgnoreCase);

        var imported = 0;
        var downloaded = 0;
        var skipped = 0;
        var failures = new List<string>();

        var index = 0;
        foreach (var file in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();
            index++;
            step?.Invoke($"正在导入 {index}/{manifest.Files.Count}：{Path.GetFileName(file.Path)}");

            var destination = Path.Combine(instanceDir, file.Path.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                if (file.Url is { Length: > 0 } url)
                {
                    if (File.Exists(destination) && new FileInfo(destination).Length > 0)
                    {
                        skipped++;
                        continue;
                    }
                    await DownloadAsync(url, destination, file.Sha1, ct).ConfigureAwait(false);
                    downloaded++;
                    continue;
                }

                if (file.FromZip is { Length: > 0 } entryName)
                {
                    if (!byName.TryGetValue(entryName, out var entry))
                    {
                        failures.Add($"{file.Path}（压缩包内缺少该条目）");
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    entry.ExtractToFile(destination, overwrite: true);
                    imported++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add($"{file.Path}（{ex.Message}）");
            }
        }

        return new ModpackImportResult(instanceId, imported, downloaded, skipped, failures.Count, failures);
    }

    // ------------------------------------------------------------ helpers

    private static string Normalize(string path)
        => path.Replace('\\', '/').TrimStart('/');

    /// <summary>Instance-relative path for an override entry, or null when it is not under the prefix.</summary>
    private static string? OverrideRelative(string entryName, string prefix)
    {
        var normalized = Normalize(entryName);
        if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var relative = normalized[prefix.Length..];
        if (relative.Length == 0 || relative.EndsWith('/'))
            return null;
        return relative;
    }

    private static string? Str(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(property, out var node)
           && node.ValueKind == JsonValueKind.String
            ? node.GetString()
            : null;

    private static async Task DownloadAsync(string url, string dest, string? sha1, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        var temp = dest + ".part";
        try
        {
            await using (var source = await Http.GetStreamAsync(url, ct).ConfigureAwait(false))
            await using (var target = File.Create(temp))
                await source.CopyToAsync(target, ct).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(sha1))
            {
                await using var stream = File.OpenRead(temp);
                var hash = SHA1.HashData(stream);
                if (!Convert.ToHexString(hash).Equals(sha1, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("SHA-1 校验失败");
            }

            File.Move(temp, dest, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch (IOException)
            {
                // leftover .part is harmless
            }
            throw;
        }
    }
}