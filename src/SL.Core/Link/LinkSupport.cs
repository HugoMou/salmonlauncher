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
using System.Security.Cryptography;

namespace SL.Core.Link;

/// <summary>Diagnostics of the link feature, forwarded to the launcher application log and to any listener.</summary>
public static class LinkLog
{
    /// <summary>Raised for every entry with a short, human-readable line (thread-pool thread).</summary>
    public static event Action<string>? Raised;

    public static void Info(string text)
    {
        Logging.AppLog.Info("Link", text);
        Raised?.Invoke(text);
    }

    public static void Warn(string text)
    {
        Logging.AppLog.Warn("Link", text);
        Raised?.Invoke("[!] " + text);
    }

    public static void Error(string text)
    {
        Logging.AppLog.Error("Link", text);
        Raised?.Invoke("[x] " + text);
    }

    public static void Error(string text, Exception ex)
    {
        Logging.AppLog.Error("Link", $"{text}:{ex.Message}");
        Raised?.Invoke($"[x] {text}:{ex.Message}");
    }
}

/// <summary>A failure the link feature understands and can show to the player as-is.</summary>
public sealed class LinkException : Exception
{
    public LinkException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>The host took this player out of the room, blocked them, or the room is full.</summary>
public sealed class RoomRejectedException : Exception
{
    public RoomRejectedException(bool blocked, bool full = false)
        : base(full ? "房间人数已满" : blocked ? "房主已屏蔽你" : "你已被房主移出房间")
    {
        Blocked = blocked;
        Full = full;
    }

    public bool Blocked { get; }
    public bool Full { get; }
}

/// <summary>Small helpers shared by the link module.</summary>
internal static class LinkUtil
{
    /// <summary>Lower-case hex string of <paramref name="byteCount"/> cryptographically random bytes.</summary>
    public static string RandomHex(int byteCount)
    {
        Span<byte> buffer = stackalloc byte[byteCount];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexStringLower(buffer);
    }

    /// <summary>Whether a string is exactly <paramref name="length"/> lower/upper hex characters.</summary>
    public static bool IsHex(string? text, int length)
    {
        if (text is null || text.Length != length)
            return false;
        foreach (char c in text)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }
        return true;
    }

    /// <summary>Copies <paramref name="source"/> into <paramref name="target"/> using only plain characters.</summary>
    public static string ClipLine(string? text, int maxLength)
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
