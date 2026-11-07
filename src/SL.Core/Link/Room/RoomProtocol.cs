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
namespace SL.Core.Link.Room;

/// <summary>
/// Request kinds of the room service, plus the fixed values both ends must agree on.
/// The service runs over one long-lived TCP connection per player.
/// </summary>
public static class RoomProtocol
{
    /// <summary>Echoes the body back; paired with <see cref="Fingerprint"/> to prove the endpoint is a room.</summary>
    public const string Ping = "c:ping";

    /// <summary>Lists the kinds the host supports, joined by NUL characters.</summary>
    public const string Protocols = "c:protocols";

    /// <summary>Returns the host's game port as two big-endian bytes.</summary>
    public const string ServerPort = "c:server_port";

    /// <summary>A guest heartbeat carrying its profile as JSON; sent every few seconds.</summary>
    public const string PlayerPing = "c:player_ping";

    /// <summary>Returns every player in the room as a JSON array.</summary>
    public const string PlayerList = "c:player_profiles_list";

    /// <summary>Extended: the game version, loader and mods of the host's game as JSON.</summary>
    public const string ModList = "campfire:mods";

    /// <summary>
    /// Extended: one chunk of a host mod. The body is the jar's SHA-1 in hex followed by the byte offset as
    /// eight big-endian bytes; the answer is up to 512 KB of the file, empty once past its end.
    /// </summary>
    public const string ModFile = "campfire:mod_file";

    /// <summary>Extended: the host's voice chat as JSON, including the UDP port of its voice server.</summary>
    public const string Voice = "campfire:voice";

    /// <summary>Extended: the room's name, announcement and player limit as JSON.</summary>
    public const string Room = "campfire:room";

    /// <summary>Virtual host name prefix of a room's host; the room service port follows it.</summary>
    public const string HostNamePrefix = "scaffolding-mc-server-";

    /// <summary>Virtual address the host holds inside its room network.</summary>
    public const string HostAddress = "10.144.144.1";

    /// <summary>Fixed marker echoed on <see cref="Ping"/> so a port scanner cannot mistake the service.</summary>
    public static readonly byte[] Fingerprint =
    [
        0x41, 0x57, 0x48, 0x44, 0x86, 0x37, 0x40, 0x59, 0x57, 0x44, 0x92, 0x43, 0x96, 0x99, 0x85, 0x01,
    ];
}
