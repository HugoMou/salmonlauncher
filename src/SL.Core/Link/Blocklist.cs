// SalmonLauncher (SL) — a Minecraft Java edition launcher
// Copyright (C) 2026 Hugo Mou
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, either version 3 of the License, or (at your option) any later version.
//
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using SL.Core.Link.Room;

namespace SL.Core.Link;

/// <summary>A player the host removed from its room and does not want back.</summary>
public sealed class BlockedPlayer
{
    public BlockedPlayer(string machineId, string name, DateTime time)
    {
        MachineId = machineId ?? "";
        Name = name ?? "";
        Time = time;
    }

    public string MachineId { get; }
    public string Name { get; }
    public DateTime Time { get; }

    public bool HasName(string name) => !string.IsNullOrEmpty(name) && string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Players blocked from this machine's rooms, remembered across sessions so a removed player cannot simply
/// rejoin with the same code.
/// </summary>
/// <remarks>
/// Written for SalmonLauncher to match the calls the ported room service makes (<see cref="Contains"/>).
/// Campfire's own list also matches players by their in-game name and UUID; that is not repeated here.
/// </remarks>
public static class Blocklist
{
    private static readonly object Gate = new();
    private static readonly List<BlockedPlayer> Entries = [];
    private static bool _loaded;

    /// <summary>Raised when the list changes (thread-pool thread).</summary>
    public static event Action? Changed;

    public static IReadOnlyList<BlockedPlayer> All
    {
        get
        {
            Load();
            lock (Gate)
                return Entries.ToArray();
        }
    }

    /// <summary>Whether the player is blocked.</summary>
    public static bool Contains(PlayerProfile profile)
        => profile is not null && ContainsId(profile.MachineId);

    public static bool ContainsId(string machineId)
    {
        if (string.IsNullOrEmpty(machineId))
            return false;
        Load();
        lock (Gate)
            return Entries.Any(e => string.Equals(e.MachineId, machineId, StringComparison.OrdinalIgnoreCase));
    }

    public static void Add(PlayerProfile profile)
    {
        if (profile is null || string.IsNullOrEmpty(profile.MachineId))
            return;
        Load();
        lock (Gate)
        {
            Entries.RemoveAll(e => string.Equals(e.MachineId, profile.MachineId, StringComparison.OrdinalIgnoreCase));
            Entries.Add(new BlockedPlayer(profile.MachineId, profile.Name, DateTime.UtcNow));
        }
        Save();
        Changed?.Invoke();
    }

    public static bool Remove(string machineId)
    {
        if (string.IsNullOrEmpty(machineId))
            return false;
        Load();
        bool removed;
        lock (Gate)
            removed = Entries.RemoveAll(e => string.Equals(e.MachineId, machineId, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed)
        {
            Save();
            Changed?.Invoke();
        }
        return removed;
    }

    private static string FilePath => Path.Combine(LinkSettings.DataDirectory, "blocklist.json");

    private static void Load()
    {
        if (_loaded)
            return;
        _loaded = true;
        try
        {
            if (!File.Exists(FilePath))
                return;
            var stored = JsonSerializer.Deserialize<List<Stored>>(File.ReadAllText(FilePath));
            if (stored is null)
                return;
            lock (Gate)
            {
                Entries.Clear();
                foreach (var item in stored)
                {
                    if (!string.IsNullOrEmpty(item.MachineId))
                        Entries.Add(new BlockedPlayer(item.MachineId, item.Name ?? "", item.Time));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            LinkLog.Warn("读取黑名单失败:" + ex.Message);
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(LinkSettings.DataDirectory);
            List<Stored> stored;
            lock (Gate)
                stored = Entries.Select(e => new Stored(e.MachineId, e.Name, e.Time)).ToList();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(stored));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LinkLog.Warn("保存黑名单失败:" + ex.Message);
        }
    }

    private sealed record Stored(string MachineId, string? Name, DateTime Time);
}
