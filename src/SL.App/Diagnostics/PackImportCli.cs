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
using SL.Core.Instances;
using SL.Core.Launch;
using SL.Core.Logging;

namespace SL.App.Diagnostics;

/// <summary>
/// Headless modpack import, started with <c>--import-pack &lt;zip&gt; [--import-into &lt;instanceId&gt;]</c>.
/// The GUI path is the normal way in; this exists so an import can be scripted and so the
/// importer can be exercised end to end without driving the window.
/// </summary>
public static class PackImportCli
{
    /// <summary>Zip or .mrpack path from the command line, or null when the mode is off.</summary>
    public static string? RequestedPack { get; } = Parse("--import-pack");

    /// <summary>Target instance id; defaults to the newest installed instance.</summary>
    public static string? RequestedInstance { get; } = Parse("--import-into");

    /// <summary>When present, build the loader instance the pack asks for instead of importing into an existing one.</summary>
    public static bool CreateInstance { get; } = Has("--create-instance");

    private static bool Has(string flag)
    {
        try
        {
            return Environment.GetCommandLineArgs()
                .Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    public static bool IsEnabled => RequestedPack is not null;

    private static string? Parse(string flag)
    {
        try
        {
            var argv = Environment.GetCommandLineArgs();
            for (var i = 0; i < argv.Length; i++)
            {
                var arg = argv[i];
                string? value = null;
                if (arg.Equals(flag, StringComparison.OrdinalIgnoreCase))
                    value = i + 1 < argv.Length ? argv[i + 1] : null;
                else if (arg.StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase))
                    value = arg[(flag.Length + 1)..];

                if (value is null)
                    continue;

                var trimmed = value.Trim().Trim('"');
                return trimmed.Length == 0 ? null : trimmed;
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("PackCli", $"解析 {flag} 失败:" + ex.Message);
        }
        return null;
    }

    /// <summary>Run the import and write a short report to stdout; returns the process exit code.</summary>
    public static async Task<int> RunAsync()
    {
        var pack = RequestedPack!;
        if (!File.Exists(pack))
        {
            Console.Error.WriteLine($"pack not found: {pack}");
            return 2;
        }

        var instanceId = RequestedInstance;
        if (string.IsNullOrWhiteSpace(instanceId))
            instanceId = InstanceRegistry.Latest()?.InstanceId;
        if (string.IsNullOrWhiteSpace(instanceId) && !CreateInstance)
        {
            Console.Error.WriteLine("no instance available; pass --import-into <instanceId> or --create-instance");
            return 3;
        }

        try
        {
            var manifest = ModpackImporter.Inspect(pack);
            Console.WriteLine($"format={manifest.Format}");
            Console.WriteLine($"name={manifest.Name}");
            Console.WriteLine($"version={manifest.Version}");
            Console.WriteLine($"minecraft={manifest.GameVersion}");
            Console.WriteLine($"loader={manifest.Loader} {manifest.LoaderVersion}".Trim());
            Console.WriteLine($"files={manifest.Files.Count} mods={manifest.ModCount}");

            if (CreateInstance)
            {
                if (!manifest.NeedsLoader || manifest.GameVersion.Length == 0)
                {
                    Console.Error.WriteLine("pack does not declare a loader + minecraft version");
                    return 6;
                }

                Console.WriteLine("step 补齐原版文件…");
                await VanillaInstaller.EnsureAsync(manifest.GameVersion, null, CancellationToken.None,
                    text => Console.WriteLine("  " + text)).ConfigureAwait(false);

                var vanillaJson = Path.Combine(Core.Boot.Runtime.InstancesDir, manifest.GameVersion, "meta",
                    manifest.GameVersion + ".json");
                var vanillaJar = Path.Combine(Core.Boot.Runtime.InstancesDir, manifest.GameVersion, "versions",
                    manifest.GameVersion + ".jar");

                var plan = await LoaderInstall.BuildPlanAsync(manifest.Loader, manifest.GameVersion,
                        manifest.LoaderVersion.Length > 0 ? manifest.LoaderVersion : null,
                        vanillaJson, vanillaJar, CancellationToken.None,
                        text => Console.WriteLine("  " + text))
                    .ConfigureAwait(false);

                instanceId = plan.InstanceId;
                Console.WriteLine($"instance={instanceId}");
            }

            var result = await ModpackImporter.ImportAsync(pack, instanceId!,
                text => Console.WriteLine("step " + text), CancellationToken.None);

            Console.WriteLine($"imported={result.Imported} downloaded={result.Downloaded} " +
                              $"skipped={result.Skipped} failed={result.Failed}");
            foreach (var failure in result.Failures.Take(10))
                Console.WriteLine("fail " + failure);

            AppLog.Info("PackCli", $"导入 {manifest.Name} → {instanceId}:" +
                                   $" {result.Imported} 解压 / {result.Downloaded} 下载 / {result.Failed} 失败");
            return result.Failed == 0 ? 0 : 4;
        }
        catch (Exception ex)
        {
            AppLog.Error("PackCli", ex);
            Console.Error.WriteLine($"import failed: {ex.GetType().Name}: {ex.Message}");
            return 5;
        }
    }
}