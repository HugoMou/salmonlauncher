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

namespace SL.Core.Link;

/// <summary>What the companion mod last reported about the running game.</summary>
/// <param name="Ready">Whether a world is open to LAN right now.</param>
/// <param name="Port">Port the game opened, 0 when unknown.</param>
/// <param name="Motd">World name the game announced.</param>
/// <param name="Players">Players in the world.</param>
/// <param name="MaxPlayers">Slot limit, 0 when unlimited.</param>
public sealed record CompanionStatus(bool Ready, int Port, string Motd, int Players, int MaxPlayers)
{
    public static readonly CompanionStatus None = new(false, 0, "", 0, 0);
}

/// <summary>
/// The bridge to the optional companion mod: the launcher tells the game where to write its world state and
/// where to read the room text, so a host can open a world without the launcher having to see a multicast
/// packet, and everybody can see the room in the tab list.
/// </summary>
/// <remarks>
/// Without the mod installed every call here is inert: the files simply never appear.
/// </remarks>
public static class CompanionBridge
{
    /// <summary>Where the mod writes the world state.</summary>
    public static string StatusFile => Path.Combine(LinkSettings.DataDirectory, "status.json");

    /// <summary>Where the launcher writes the room text the mod shows in game.</summary>
    public static string RoomFile => Path.Combine(LinkSettings.DataDirectory, "room.json");

    /// <summary>Folder the installer ships the mod jar in, next to the launcher executable.</summary>
    public static string BundledDirectory => Path.Combine(AppContext.BaseDirectory, "companion");

    /// <summary>JVM arguments that point the mod at both files; empty when no mod is bundled.</summary>
    public static string JvmArguments()
    {
        Directory.CreateDirectory(LinkSettings.DataDirectory);
        return $"-Dsl.link.status=\"{StatusFile}\" -Dsl.link.room=\"{RoomFile}\"";
    }

    /// <summary>
    /// Copies the bundled mod into an instance when it is not there yet. Returns the file name of the mod,
    /// or an empty string when this build ships no companion jar.
    /// </summary>
    public static string EnsureInstalled(string instanceId)
    {
        if (instanceId.Length == 0 || !Directory.Exists(BundledDirectory))
            return "";

        string? jar = Directory.EnumerateFiles(BundledDirectory, "*.jar", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (jar is null)
            return "";

        string name = Path.GetFileName(jar);
        string mods = Path.Combine(Boot.Runtime.InstancesDir, instanceId, "mods");
        try
        {
            Directory.CreateDirectory(mods);
            string target = Path.Combine(mods, name);
            if (!File.Exists(target) || new FileInfo(target).Length != new FileInfo(jar).Length)
            {
                File.Copy(jar, target, overwrite: true);
                LinkLog.Info("已为实例安装联机伴随模组:" + name);
            }
            return name;
        }
        catch (IOException ex)
        {
            LinkLog.Warn("安装伴随模组失败:" + ex.Message);
            return "";
        }
    }

    /// <summary>Reads the latest status the mod wrote; <see cref="CompanionStatus.None"/> when there is none.</summary>
    public static CompanionStatus ReadStatus()
    {
        try
        {
            if (!File.Exists(StatusFile))
                return CompanionStatus.None;

            using var document = JsonDocument.Parse(File.ReadAllText(StatusFile));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return CompanionStatus.None;

            bool ready = root.TryGetProperty("ready", out var readyElement) && readyElement.ValueKind == JsonValueKind.True;
            int port = Int(root, "port");
            int players = Int(root, "players");
            int maxPlayers = Int(root, "maxPlayers");
            string motd = root.TryGetProperty("motd", out var motdElement) && motdElement.ValueKind == JsonValueKind.String
                ? motdElement.GetString() ?? ""
                : "";
            return new CompanionStatus(ready && port is >= 1 and <= 65535, port, motd, players, maxPlayers);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return CompanionStatus.None;
        }
    }

    /// <summary>Publishes the room text the mod shows in the tab list while a room is open.</summary>
    public static void WriteRoom(string roomName, string hostName, IEnumerable<string> players)
    {
        try
        {
            Directory.CreateDirectory(LinkSettings.DataDirectory);
            var payload = new RoomText(roomName, hostName, players.Take(64).ToArray());
            File.WriteAllText(RoomFile, JsonSerializer.Serialize(payload, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LinkLog.Warn("写入房间信息失败:" + ex.Message);
        }
    }

    /// <summary>Removes the room text so the mod stops showing it.</summary>
    public static void ClearRoom()
    {
        try
        {
            if (File.Exists(RoomFile))
                File.Delete(RoomFile);
        }
        catch (IOException)
        {
            // a leftover file only affects the tab list of a game that is already closing
        }
    }

    private static int Int(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)
            ? number
            : 0;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private sealed record RoomText(
        [property: JsonPropertyName("roomName")] string RoomName,
        [property: JsonPropertyName("hostName")] string HostName,
        [property: JsonPropertyName("players")] IReadOnlyList<string> Players);
}
