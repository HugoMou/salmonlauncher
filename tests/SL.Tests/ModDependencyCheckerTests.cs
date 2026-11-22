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

namespace SL.Tests;

/// <summary>模组依赖检查：构造真实 jar 结构，验证解析与问题识别。</summary>
public sealed class ModDependencyCheckerTests : IDisposable
{
    private readonly string _dir;

    public ModDependencyCheckerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "sl-test-mods-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // 测试清理失败不影响结论
        }
    }

    private string CreateFabricJar(string fileName, string id, string version, params string[] depends)
    {
        var path = Path.Combine(_dir, fileName);
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

        var entry = zip.CreateEntry("fabric.mod.json");
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(json);
        stream.Write(bytes, 0, bytes.Length);

        return path;
    }

    [Fact]
    public void Describe_ReadsFabricMetadata()
    {
        var jar = CreateFabricJar("sodium.jar", "sodium", "0.5.8", "minecraft");
        var descriptor = ModDependencyChecker.Describe(jar);

        Assert.NotNull(descriptor);
        Assert.Equal("sodium", descriptor!.ModId);
        Assert.Equal("0.5.8", descriptor.Version);
        Assert.Equal("fabric", descriptor.Loader);
        Assert.Contains("minecraft", descriptor.DependsOn);
    }

    [Fact]
    public void Check_ReportsMissingDependencyAsError()
    {
        CreateFabricJar("iris.jar", "iris", "1.7.0", "sodium");

        var issues = ModDependencyChecker.Check(_dir);

        Assert.Contains(issues, i => i.Level == ModIssueLevel.Error && i.Message.Contains("sodium"));
    }

    [Fact]
    public void Check_PassesWhenDependencyPresent()
    {
        CreateFabricJar("sodium.jar", "sodium", "0.5.8", "minecraft");
        CreateFabricJar("iris.jar", "iris", "1.7.0", "sodium");

        var issues = ModDependencyChecker.Check(_dir);

        Assert.DoesNotContain(issues, i => i.Level == ModIssueLevel.Error);
    }

    [Fact]
    public void Check_IgnoresBuiltinDependencies()
    {
        CreateFabricJar("lithium.jar", "lithium", "0.13", "minecraft", "java", "fabricloader", "fabric-api");

        var issues = ModDependencyChecker.Check(_dir);

        Assert.Empty(issues);
    }

    [Fact]
    public void Check_ReportsDuplicateModIdAsWarning()
    {
        CreateFabricJar("sodium-1.jar", "sodium", "0.5.8");
        CreateFabricJar("sodium-2.jar", "sodium", "0.6.0");

        var issues = ModDependencyChecker.Check(_dir);

        Assert.Contains(issues, i => i.Level == ModIssueLevel.Warning && i.ModId == "sodium");
    }

    [Fact]
    public void Check_ReportsUnparsableJarAsInfo()
    {
        var path = Path.Combine(_dir, "random.jar");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("readme.txt");
            using var stream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes("no metadata here");
            stream.Write(bytes, 0, bytes.Length);
        }

        var issues = ModDependencyChecker.Check(_dir);

        Assert.Contains(issues, i => i.Level == ModIssueLevel.Info);
    }

    [Fact]
    public void Scan_ReturnsOnlyParsableMods()
    {
        CreateFabricJar("a.jar", "alpha", "1.0");
        var junk = Path.Combine(_dir, "junk.jar");
        File.WriteAllText(junk, "not a zip");

        var mods = ModDependencyChecker.Scan(_dir);

        Assert.Single(mods);
        Assert.Equal("alpha", mods[0].ModId);
    }

    [Fact]
    public void Summarize_DescribesEmptyAndNonEmptyResults()
    {
        Assert.Equal("模组依赖检查通过", ModDependencyChecker.Summarize(Array.Empty<ModIssue>()));

        var issues = new[]
        {
            new ModIssue(ModIssueLevel.Error, "iris", "x"),
            new ModIssue(ModIssueLevel.Warning, "sodium", "y"),
        };

        var text = ModDependencyChecker.Summarize(issues);
        Assert.Contains("2", text);
    }

    [Fact]
    public void Check_ReturnsEmptyForMissingDirectory()
    {
        var issues = ModDependencyChecker.Check(Path.Combine(_dir, "does-not-exist"));
        Assert.Empty(issues);
    }
}