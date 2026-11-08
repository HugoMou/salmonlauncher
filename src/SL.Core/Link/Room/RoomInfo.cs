// SalmonLauncher (SL) — a Minecraft Java edition launcher
// Copyright (C) 2026 Hugo Mou
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, either version 3 of the License, or (at your option) any later version.
//
// SPDX-License-Identifier: GPL-3.0-or-later
namespace SL.Core.Link.Room;

/// <summary>The room's name, announcement and player limit, as players receive them.</summary>
/// <remarks>
/// Written for SalmonLauncher to match the calls Campfire's room service makes on this type. The field names
/// on the wire follow the ported protocol.
/// </remarks>
public sealed class RoomInfo
{
    public const int MaxName = 48;
    public const int MaxNotice = 200;

    public RoomInfo(string? name, string? notice, int maxPlayers)
    {
        Name = Clip(name, MaxName);
        Notice = Clip(notice, MaxNotice);
        MaxPlayers = maxPlayers is >= 2 and <= 100 ? maxPlayers : 0;
    }

    public string Name { get; }

    /// <summary>The announcement, or an empty text when there is none.</summary>
    public string Notice { get; }

    /// <summary>Most players the room takes, host included; 0 means no limit.</summary>
    public int MaxPlayers { get; }

    public Dictionary<string, object?> ToJson() => new()
    {
        ["name"] = Name,
        ["notice"] = Notice,
        ["max"] = MaxPlayers,
    };

    /// <summary>Reads what the host sent; <c>null</c> when the value is not an object.</summary>
    public static RoomInfo? FromJson(object? value)
    {
        if (value is not IDictionary<string, object?> json)
            return null;

        string name = json.TryGetValue("name", out var n) ? n as string ?? "" : "";
        string notice = json.TryGetValue("notice", out var t) ? t as string ?? "" : "";
        int max = json.TryGetValue("max", out var m) && m is double d ? (int)d : 0;
        return new RoomInfo(name, notice, max);
    }

    private static string Clip(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var clean = new System.Text.StringBuilder(Math.Min(text.Length, maxLength));
        foreach (char c in text.Trim())
        {
            if (clean.Length >= maxLength)
                break;
            clean.Append(char.IsControl(c) ? ' ' : c);
        }
        return clean.ToString();
    }
}
