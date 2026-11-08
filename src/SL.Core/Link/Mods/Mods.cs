// SalmonLauncher (SL) — a Minecraft Java edition launcher
// Copyright (C) 2026 Hugo Mou
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, either version 3 of the License, or (at your option) any later version.
//
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;

namespace SL.Core.Link.Mods;

/// <summary>One mod the host offers to guests.</summary>
public sealed class ModFile
{
    public ModFile(string name, string sha1, long size)
    {
        Name = name ?? "";
        Sha1 = (sha1 ?? "").ToLowerInvariant();
        Size = size;
    }

    public string Name { get; }

    /// <summary>SHA-1 of the file, lower-case hex.</summary>
    public string Sha1 { get; }

    public long Size { get; }

    internal Dictionary<string, object?> ToJson() => new()
    {
        ["name"] = Name,
        ["sha1"] = Sha1,
        ["size"] = (double)Size,
    };

    internal static ModFile? FromJson(object? value)
    {
        if (value is not IDictionary<string, object?> json)
            return null;
        if (!(Get(json, "name") is string name) || name.Length == 0)
            return null;
        if (!(Get(json, "sha1") is string sha1) || sha1.Length == 0)
            return null;
        long size = Get(json, "size") is double d && d >= 0 ? (long)d : 0;
        return new ModFile(name, sha1, size);
    }

    internal static object? Get(IDictionary<string, object?> json, string key)
        => json.TryGetValue(key, out var value) ? value : null;
}

/// <summary>The host's game version, loader and mods as guests receive them.</summary>
public sealed class ModList
{
    public ModList(string gameVersion, string loader, IReadOnlyList<ModFile> files)
    {
        GameVersion = gameVersion ?? "";
        Loader = loader ?? "";
        Files = files ?? [];
    }

    public string GameVersion { get; }

    public string Loader { get; }

    public IReadOnlyList<ModFile> Files { get; }

    public Dictionary<string, object?> ToJson()
    {
        var mods = new List<object?>();
        foreach (var file in Files)
            mods.Add(file.ToJson());
        return new Dictionary<string, object?>
        {
            ["game"] = GameVersion,
            ["loader"] = Loader,
            ["mods"] = mods,
        };
    }

    /// <summary>Reads a mod list; <c>null</c> when the value is not an object.</summary>
    public static ModList? FromJson(object? value)
    {
        if (value is not IDictionary<string, object?> json)
            return null;

        string game = ModFile.Get(json, "game") as string ?? "";
        string loader = ModFile.Get(json, "loader") as string ?? "";
        var files = new List<ModFile>();
        if (ModFile.Get(json, "mods") is IEnumerable<object?> items)
        {
            foreach (var item in items)
            {
                var file = ModFile.FromJson(item);
                if (file is not null)
                    files.Add(file);
            }
        }
        return new ModList(game, loader, files);
    }
}

/// <summary>The host's voice chat as guests receive it; <c>null</c> on the wire when it is off.</summary>
public sealed class VoiceOffer
{
    public VoiceOffer(bool enabled, int port)
    {
        Enabled = enabled;
        Port = port;
    }

    public bool Enabled { get; }

    public int Port { get; }

    public Dictionary<string, object?> ToJson() => new()
    {
        ["enabled"] = Enabled,
        ["port"] = (double)Port,
    };

    public static VoiceOffer? FromJson(object? value)
    {
        if (value is not IDictionary<string, object?> json)
            return null;
        bool enabled = ModFile.Get(json, "enabled") is true;
        int port = ModFile.Get(json, "port") is double d && d is >= 0 and <= 65535 ? (int)d : 0;
        return new VoiceOffer(enabled, port);
    }
}

/// <summary>
/// The host's shared mods, answering the two requests the room service forwards: the list itself and one
/// chunk of one file.
/// </summary>
public sealed class ModHost
{
    private const int ChunkSize = 512 * 1024;

    private readonly Func<string, string?> _pathFor;

    public ModHost(ModList list, Func<string, string?> pathFor)
    {
        List = list ?? new ModList("", "", []);
        _pathFor = pathFor;
        ListJson = Encoding.UTF8.GetBytes(Json.Serialize(List.ToJson()));
    }

    /// <summary>What is served; empty when the host shares nothing.</summary>
    public ModList List { get; }

    /// <summary>The list as the room service sends it, serialised once.</summary>
    public byte[] ListJson { get; }

    /// <summary>One slice of a shared file; <c>null</c> when the file is unknown or unreadable.</summary>
    public byte[]? ReadChunk(string sha1, long offset)
    {
        if (string.IsNullOrEmpty(sha1) || offset < 0)
            return null;

        string? path = _pathFor(sha1.ToLowerInvariant());
        if (path is null || !File.Exists(path))
            return null;

        try
        {
            using var file = File.OpenRead(path);
            if (offset >= file.Length)
                return [];
            int count = (int)Math.Min(ChunkSize, file.Length - offset);
            var buffer = new byte[count];
            file.Seek(offset, SeekOrigin.Begin);
            int read = file.Read(buffer, 0, count);
            return read == count ? buffer : buffer[..read];
        }
        catch (IOException)
        {
            return null;
        }
    }
}
