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
using System.Text;
using SL.Core.Instances;
using SL.Core.Launch;

namespace SL.SelfCheck;

/// <summary>
/// 无测试框架的确定性自检：在无法启动 vstest 宿主的环境里，仍然真实执行核心逻辑断言。
/// 退出码 0 表示全部通过。
/// </summary>
internal static class Program
{
    private static int _passed;
    private static readonly List<string> Failures = [];

    private static void Check(string name, bool condition, string? detail = null)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"  PASS  {name}");
            return;
        }

        Failures.Add(name + (detail is null ? "" : " :: " + detail));
        Console.WriteLine($"  FAIL  {name}{(detail is null ? "" : " :: " + detail)}");
    }

    private static void CheckEqual<T>(string name, T expected, T actual)
        => Check(name, EqualityComparer<T>.Default.Equals(expected, actual), $"expected={expected} actual={actual}");

    private static int Main()
    {
        // 沙箱只允许写程序目录：把数据目录也固定到 bin 下，避免 AppData 被拒
        Environment.SetEnvironmentVariable("SL_DATA_DIR",
            Path.Combine(AppContext.BaseDirectory, "selfcheck-data"));
        // 每次从干净的数据目录开始，避免上一次运行的残留实例影响断言
        var dataRoot = Path.Combine(AppContext.BaseDirectory, "selfcheck-data");
        try
        {
            if (Directory.Exists(dataRoot))
                Directory.Delete(dataRoot, recursive: true);
        }
        catch
        {
            // 清理失败不影响后续断言
        }

        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("SalmonLauncher 自检");
        Console.WriteLine("====================");

        JavaRuntimeChecks();
        ModDependencyChecks();
        ZipRoundTripChecks();

        Console.WriteLine("====================");
        Console.WriteLine($"通过 {_passed} 项，失败 {Failures.Count} 项");
        foreach (var failure in Failures)
            Console.WriteLine("  ! " + failure);

        return Failures.Count == 0 ? 0 : 1;
    }

    private static void JavaRuntimeChecks()
    {
        Console.WriteLine("Java 主版本推断");
        CheckEqual("1.7.10 -> 8", 8, JavaRuntime.RequiredMajorFor("1.7.10"));
        CheckEqual("1.8.9 -> 8", 8, JavaRuntime.RequiredMajorFor("1.8.9"));
        CheckEqual("1.12.2 -> 8", 8, JavaRuntime.RequiredMajorFor("1.12.2"));
        CheckEqual("1.16.5 -> 8", 8, JavaRuntime.RequiredMajorFor("1.16.5"));
        CheckEqual("1.17 -> 17", 17, JavaRuntime.RequiredMajorFor("1.17"));
        CheckEqual("1.18.2 -> 17", 17, JavaRuntime.RequiredMajorFor("1.18.2"));
        CheckEqual("1.20.1 -> 17", 17, JavaRuntime.RequiredMajorFor("1.20.1"));
        CheckEqual("1.20.4 -> 17", 17, JavaRuntime.RequiredMajorFor("1.20.4"));
        CheckEqual("1.20.5 -> 21", 21, JavaRuntime.RequiredMajorFor("1.20.5"));
        CheckEqual("1.21.4 -> 21", 21, JavaRuntime.RequiredMajorFor("1.21.4"));
        CheckEqual("1.21.4-fabric -> 21", 21, JavaRuntime.RequiredMajorFor("1.21.4-fabric"));
        CheckEqual("1.20.1-forge -> 17", 17, JavaRuntime.RequiredMajorFor("1.20.1-forge"));
        CheckEqual("snapshot name -> 21", 21, JavaRuntime.RequiredMajorFor("24w14a"));
        CheckEqual("empty -> 21", 21, JavaRuntime.RequiredMajorFor(""));

        var display = new JavaRuntimeInfo(@"C:\java\bin\java.exe", "17.0.9", 17, true).Display;
        Check("Display 含主版本与版本号", display.Contains("17") && display.Contains("17.0.9"), display);

        var detected = JavaRuntime.Detect(forceRefresh: true);
        Console.WriteLine($"  INFO  本机检测到 {detected.Count} 个 Java 运行时");
    }

    private static void ModDependencyChecks()
    {
        Console.WriteLine("模组依赖检查");

        var dir = ScratchDir("mods");
        Directory.CreateDirectory(dir);

        try
        {
            WriteFabricJar(dir, "sodium.jar", "sodium", "0.5.8", "minecraft");
            WriteFabricJar(dir, "iris.jar", "iris", "1.7.0", "sodium");

            var descriptor = ModDependencyChecker.Describe(Path.Combine(dir, "sodium.jar"));
            Check("Describe 解析 fabric.mod.json", descriptor is not null);
            CheckEqual("ModId", "sodium", descriptor?.ModId ?? "");
            CheckEqual("Version", "0.5.8", descriptor?.Version ?? "");
            CheckEqual("Loader", "fabric", descriptor?.Loader ?? "");

            var ok = ModDependencyChecker.Check(dir);
            Check("依赖齐全时无 Error", ok.All(i => i.Level != ModIssueLevel.Error),
                string.Join(" | ", ok.Select(i => i.Message)));

            // 移除 sodium，制造缺失依赖
            File.Delete(Path.Combine(dir, "sodium.jar"));
            var missing = ModDependencyChecker.Check(dir);
            Check("缺失依赖报 Error", missing.Any(i => i.Level == ModIssueLevel.Error && i.Message.Contains("sodium")),
                string.Join(" | ", missing.Select(i => i.Message)));

            // 重复 modId
            var dir2 = Path.Combine(dir, "dup");
            Directory.CreateDirectory(dir2);
            WriteFabricJar(dir2, "a.jar", "lithium", "0.13");
            WriteFabricJar(dir2, "b.jar", "lithium", "0.14");
            var dup = ModDependencyChecker.Check(dir2);
            Check("重复 modId 报 Warning", dup.Any(i => i.Level == ModIssueLevel.Warning && i.ModId == "lithium"),
                string.Join(" | ", dup.Select(i => i.Message)));

            // 内置依赖跳过
            var dir3 = Path.Combine(dir, "builtin");
            Directory.CreateDirectory(dir3);
            WriteFabricJar(dir3, "c.jar", "ferritecore", "6.0", "minecraft", "java", "fabricloader");
            Check("内置依赖不报错", ModDependencyChecker.Check(dir3).Count == 0);

            // 损坏 jar
            var dir4 = Path.Combine(dir, "broken");
            Directory.CreateDirectory(dir4);
            File.WriteAllText(Path.Combine(dir4, "junk.jar"), "not a zip");
            var broken = ModDependencyChecker.Check(dir4);
            Check("无法识别的 jar 报 Info", broken.Any(i => i.Level == ModIssueLevel.Info));

            CheckEqual("空目录摘要", "模组依赖检查通过",
                ModDependencyChecker.Summarize(Array.Empty<ModIssue>()));
            Check("非空摘要含数量", ModDependencyChecker.Summarize(missing).Contains(missing.Count.ToString()));
        }
        finally
        {
            TryDelete(dir);
        }
    }

    private static void ZipRoundTripChecks()
    {
        Console.WriteLine("实例 zip 导入导出");
        // 受限环境：把 Core 的临时目录固定到程序目录，避免被沙箱拒绝
        InstanceRegistry.ScratchRootOverride = Path.Combine(AppContext.BaseDirectory, "selfcheck-tmp");


        var dir = ScratchDir("zip");
        Directory.CreateDirectory(dir);

        try
        {
            Check("缺失实例导出返回 null",
                InstanceRegistry.ExportInstanceZip("no-such-instance-" + Guid.NewGuid().ToString("N")) is null);

            var missingZip = Path.Combine(dir, "absent.zip");
            var threw = false;
            try
            {
                InstanceRegistry.ImportInstanceZip(missingZip);
            }
            catch (FileNotFoundException)
            {
                threw = true;
            }
            Check("导入不存在的 zip 抛 FileNotFoundException", threw);

            // meta/instance.json 优先作为实例 id
            var withMeta = Path.Combine(dir, "with-meta.zip");
            using (var zip = ZipFile.Open(withMeta, ZipArchiveMode.Create))
            {
                WriteEntry(zip, "meta/instance.json", "{\"id\":\"1.21.4-fabric\"}");
                WriteEntry(zip, "mods/example.jar", "jar-bytes");
            }

            var idFromMeta = InstanceRegistry.ImportInstanceZip(withMeta);
            Check("从 meta 读取实例 id", !string.IsNullOrWhiteSpace(idFromMeta) && idFromMeta!.Contains("1.21.4-fabric"),
                idFromMeta ?? "null");

            // 无 meta 时退回文件名
            var noMeta = Path.Combine(dir, "my-backup.zip");
            using (var zip = ZipFile.Open(noMeta, ZipArchiveMode.Create))
                WriteEntry(zip, "mods/example.jar", "jar-bytes");

            CheckEqual("无 meta 回退文件名", "my-backup", InstanceRegistry.ImportInstanceZip(noMeta));

            // 指定目标实例
            var restore = Path.Combine(dir, "restore.zip");
            using (var zip = ZipFile.Open(restore, ZipArchiveMode.Create))
                WriteEntry(zip, "meta/instance.json", "{\"id\":\"source\"}");

            var target = InstanceRegistry.ImportInstanceZip(restore, "target-instance");
            Check("指定目标实例导入成功", !string.IsNullOrWhiteSpace(target), target ?? "null");
        }
        finally
        {
            TryDelete(dir);
        }
    }


    private static string ScratchDir(string kind)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "selfcheck-tmp", kind + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteFabricJar(string dir, string fileName, string id, string version, params string[] depends)
    {
        var path = Path.Combine(dir, fileName);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

        var deps = new StringBuilder();
        for (var i = 0; i < depends.Length; i++)
        {
            if (i > 0)
                deps.Append(',');
            deps.Append('"').Append(depends[i]).Append("\":\">=1.0.0\"");
        }

        var json = "{\"schemaVersion\":1,\"id\":\"" + id + "\",\"version\":\"" + version + "\"," +
                   "\"name\":\"" + id + " mod\",\"depends\":{" + deps + "}}";

        WriteEntry(zip, "fabric.mod.json", json);
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // 忽略
        }
    }
}