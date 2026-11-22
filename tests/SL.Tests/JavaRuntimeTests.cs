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
using SL.Core.Launch;

namespace SL.Tests;

/// <summary>Java 主版本推断：不同 Minecraft 版本区间必须选中正确的 Java。</summary>
public sealed class JavaRuntimeTests
{
    [Theory]
    [InlineData("1.7.10", 8)]
    [InlineData("1.8.9", 8)]
    [InlineData("1.12.2", 8)]
    [InlineData("1.16.5", 8)]
    [InlineData("1.17", 17)]
    [InlineData("1.18.2", 17)]
    [InlineData("1.20.1", 17)]
    [InlineData("1.20.4", 17)]
    [InlineData("1.20.5", 21)]
    [InlineData("1.20.6", 21)]
    [InlineData("1.21", 21)]
    [InlineData("1.21.4", 21)]
    public void RequiredMajorFor_MapsMinecraftVersionToJavaMajor(string gameVersion, int expected)
    {
        Assert.Equal(expected, JavaRuntime.RequiredMajorFor(gameVersion));
    }

    [Fact]
    public void RequiredMajorFor_IgnoresLoaderSuffix()
    {
        Assert.Equal(21, JavaRuntime.RequiredMajorFor("1.21.4-fabric"));
        Assert.Equal(17, JavaRuntime.RequiredMajorFor("1.20.1-forge"));
        Assert.Equal(8, JavaRuntime.RequiredMajorFor("1.12.2-neoforge"));
    }

    [Fact]
    public void RequiredMajorFor_ReturnsModernDefaultForSnapshotNames()
    {
        // 快照名（如 24w14a）不是数字开头，回落到当前推荐的 Java 21
        Assert.Equal(21, JavaRuntime.RequiredMajorFor("24w14a"));
        Assert.Equal(21, JavaRuntime.RequiredMajorFor(""));
    }

    [Fact]
    public void JavaRuntimeInfo_DisplayMentionsMajorAndVersion()
    {
        var info = new JavaRuntimeInfo(@"C:\java\bin\java.exe", "17.0.9", 17, true);
        Assert.Contains("17", info.Display);
        Assert.True(info.IsJdk);
    }
}