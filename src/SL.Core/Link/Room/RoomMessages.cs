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
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SL.Core.Link.Room;

/// <summary>Bodies carried by <see cref="RoomProtocol"/> requests, one helper per request kind.</summary>
public static class RoomMessages
{
    /// <summary>The host's game port as two big-endian bytes.</summary>
    public static byte[] ServerPort(int port)
    {
        var body = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(body, (ushort)Math.Clamp(port, 1, 65535));
        return body;
    }

    /// <summary>Reads <see cref="ServerPort"/>; 0 when the body is unusable.</summary>
    public static int ReadServerPort(byte[] body)
        => body.Length == 2 ? BinaryPrimitives.ReadUInt16BigEndian(body) : 0;

    /// <summary>Kinds the host supports, joined by NUL characters.</summary>
    public static byte[] ProtocolList(IEnumerable<string> kinds)
        => Encoding.UTF8.GetBytes(string.Join('\0', kinds));

    /// <summary>Reads <see cref="ProtocolList"/>.</summary>
    public static string[] ReadProtocolList(byte[] body)
        => Encoding.UTF8.GetString(body).Split('\0', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Players of a room as a JSON array.</summary>
    public static byte[] PlayerList(IEnumerable<PlayerProfile> players)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var player in players)
                writer.WriteRawValue(player.ToJson(includeKind: false), skipInputValidation: true);
            writer.WriteEndArray();
        }
        return stream.ToArray();
    }

    /// <summary>Reads a player list; entries without an id or name are skipped.</summary>
    public static List<PlayerProfile> ReadPlayerList(byte[] body)
    {
        var players = new List<PlayerProfile>();
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return players;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var profile = PlayerProfile.FromJson(element, forcedId: null);
                if (profile is not null)
                    players.Add(profile);
            }
        }
        catch (JsonException)
        {
            // an unreadable list is reported as empty rather than failing the whole request
        }
        return players;
    }

    /// <summary>Why the host turned a player away; the client maps these to <see cref="RoomRejectedException"/>.</summary>
    public static class Rejection
    {
        public const string Full = "full";
        public const string Blocked = "blocked";
        public const string Removed = "removed";
    }
}

/// <summary>One mod the host offers to guests.</summary>
public sealed record ModEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("sha1")] string Sha1,
    [property: JsonPropertyName("size")] long Size);

/// <summary>The host's game version, loader and mods.</summary>
public sealed record ModList(
    [property: JsonPropertyName("game")] string GameVersion,
    [property: JsonPropertyName("loader")] string Loader,
    [property: JsonPropertyName("mods")] IReadOnlyList<ModEntry> Mods)
{
    public static readonly ModList Empty = new("", "", []);

    public byte[] ToJson() => JsonSerializer.SerializeToUtf8Bytes(this, LinkJson.Options);

    /// <summary>Reads a mod list; never returns <c>null</c>, an unusable body becomes <see cref="Empty"/>.</summary>
    public static ModList FromJson(byte[] body)
    {
        try
        {
            var list = JsonSerializer.Deserialize<ModList>(body, LinkJson.Options);
            return list is null ? Empty : list with { Mods = list.Mods ?? [] };
        }
        catch (JsonException)
        {
            return Empty;
        }
    }
}

/// <summary>Address of the host's voice chat inside the room network; <see cref="Enabled"/> false when it is off.</summary>
public sealed record VoiceInfo(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("port")] int Port)
{
    public static readonly VoiceInfo Off = new(false, 0);

    public byte[] ToJson() => JsonSerializer.SerializeToUtf8Bytes(this, LinkJson.Options);

    public static VoiceInfo FromJson(byte[] body)
    {
        try
        {
            return JsonSerializer.Deserialize<VoiceInfo>(body, LinkJson.Options) ?? Off;
        }
        catch (JsonException)
        {
            return Off;
        }
    }
}
