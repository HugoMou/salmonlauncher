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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SL.Core.Link.Room;

/// <summary>The room's name, announcement and player limit, as the host configured them.</summary>
public sealed class RoomInfo
{
    public const int MaxName = 48;
    public const int MaxNotice = 200;

    public RoomInfo(string? name, string? notice, int maxPlayers)
    {
        Name = LinkUtil.ClipLine(name, MaxName);
        Notice = LinkUtil.ClipLine(notice, MaxNotice);
        MaxPlayers = maxPlayers is >= 2 and <= 100 ? maxPlayers : 0;
    }

    public string Name { get; }

    /// <summary>The announcement, or an empty text when there is none.</summary>
    public string Notice { get; }

    /// <summary>Most players the room takes, host included; 0 means no limit.</summary>
    public int MaxPlayers { get; }

    public byte[] ToJson()
    {
        var payload = new WireRoom(Name, Notice, MaxPlayers);
        return JsonSerializer.SerializeToUtf8Bytes(payload, LinkJson.Options);
    }

    /// <summary>Reads what the host sent; returns <c>null</c> when the payload is not an object.</summary>
    public static RoomInfo? FromJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        string name = element.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
        string notice = element.TryGetProperty("notice", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
        int max = element.TryGetProperty("max", out var m) && m.ValueKind == JsonValueKind.Number && m.TryGetInt32(out int v) && v is >= 0 and <= 100 ? v : 0;
        return new RoomInfo(name, notice, max);
    }

    private sealed record WireRoom(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("notice")] string Notice,
        [property: JsonPropertyName("max")] int Max);
}
