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

/// <summary>实例目录与 zip 导入导出的边界行为。</summary>
public sealed class InstanceRegistryZipTests : IDisposable
{
    private readonly string _root;

    public InstanceRegistryZipTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "sl-test-inst-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // 忽略清理失败
        }
    }

    [Fact]
    public void ExportInstanceZip_ReturnsNullForMissingInstance()
    {
        var zip = InstanceRegistry.ExportInstanceZip("definitely-not-installed-" + Guid.NewGuid().ToString("N"));
        Assert.Null(zip);
    }

    [Fact]
    public void ImportInstanceZip_ThrowsForMissingFile()
    {
        var missing = Path.Combine(_root, "nope.zip");
        Assert.Throws<FileNotFoundException>(() => InstanceRegistry.ImportInstanceZip(missing));
    }

    [Fact]
    public void ImportInstanceZip_ReadsInstanceIdFromMetaJson()
    {
        var zipPath = Path.Combine(_root, "backup.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var meta = zip.CreateEntry("meta/instance.json");
            using (var stream = meta.Open())
            {
                var bytes = Encoding.UTF8.GetBytes("{\"id\":\"1.21.4-fabric\"}");
                stream.Write(bytes, 0, bytes.Length);
            }

            var mod = zip.CreateEntry("mods/example.jar");
            using (var stream = mod.Open())
            {
                var bytes = Encoding.UTF8.GetBytes("jar-bytes");
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        var instanceId = InstanceRegistry.ImportInstanceZip(zipPath);

        Assert.False(string.IsNullOrWhiteSpace(instanceId));
        Assert.Contains("1.21.4-fabric", instanceId);
    }

    [Fact]
    public void ImportInstanceZip_FallsBackToFileNameWhenMetaMissing()
    {
        var zipPath = Path.Combine(_root, "my-backup.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var mod = zip.CreateEntry("mods/example.jar");
            using var stream = mod.Open();
            var bytes = Encoding.UTF8.GetBytes("jar-bytes");
            stream.Write(bytes, 0, bytes.Length);
        }

        var instanceId = InstanceRegistry.ImportInstanceZip(zipPath);

        Assert.Equal("my-backup", instanceId);
    }

    [Fact]
    public void ImportInstanceZip_HonoursExplicitTarget()
    {
        var zipPath = Path.Combine(_root, "restore.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var meta = zip.CreateEntry("meta/instance.json");
            using var stream = meta.Open();
            var bytes = Encoding.UTF8.GetBytes("{\"id\":\"source-instance\"}");
            stream.Write(bytes, 0, bytes.Length);
        }

        var instanceId = InstanceRegistry.ImportInstanceZip(zipPath, "target-instance");

        Assert.False(string.IsNullOrWhiteSpace(instanceId));
    }
}