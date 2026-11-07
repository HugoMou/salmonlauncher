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
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SL.Core.Link;

/// <summary>
/// The invite code of a room, written as <c>U/XXXX-XXXX-XXXX-XXXX</c>.
/// </summary>
/// <remarks>
/// The body is sixteen base-34 digits of a number that is a multiple of seven, least significant digit first.
/// The first eight digits seed the virtual network name; the last eight become its secret. The code is
/// self-contained: a guest needs no server to work out where and how to connect.
/// </remarks>
public sealed class RoomCode
{
    /// <summary>Base-34 alphabet with the look-alike letters I and O left out.</summary>
    private const string Alphabet = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    private const string NetworkPrefix = "salmon-mc-";
    private const int BodyDigits = 16;
    private const int CheckDivisor = 7;

    /// <summary>Length of the full text <c>U/XXXX-XXXX-XXXX-XXXX</c>.</summary>
    private const int WireLength = 21;

    private static readonly BigInteger CarryBase = BigInteger.Pow(Alphabet.Length, BodyDigits);

    private RoomCode(BigInteger number)
    {
        // little-endian digit extraction: digit 0 is the least significant
        var body = new char[BodyDigits];
        for (int i = 0; i < BodyDigits; i++)
        {
            body[i] = Alphabet[(int)(number % Alphabet.Length)];
            number /= Alphabet.Length;
        }

        Code = BuildText(body);
        NetworkName = NetworkPrefix + Slice(body, 0) + "-" + Slice(body, 4);
        NetworkSecret = Slice(body, 8) + "-" + Slice(body, 12);
    }

    /// <summary>The code as shown to players, e.g. <c>U/1A2B-3C4D-5E6F-7G8H</c>.</summary>
    public string Code { get; }

    /// <summary>EasyTier network name derived from the first half of the code.</summary>
    public string NetworkName { get; }

    /// <summary>EasyTier network secret derived from the second half of the code.</summary>
    public string NetworkSecret { get; }

    public override string ToString() => Code;

    /// <summary>Draws a fresh code for a new room.</summary>
    public static RoomCode Create()
    {
        // A trailing zero byte keeps the little-endian BigInteger from being read as negative.
        Span<byte> buffer = stackalloc byte[17];
        RandomNumberGenerator.Fill(buffer[..16]);

        var number = new BigInteger(buffer) % CarryBase;
        return new RoomCode(number - number % CheckDivisor);
    }

    /// <summary>
    /// Picks the first valid code out of <paramref name="text"/>; the text may be a whole chat line.
    /// Matching ignores case and reads the look-alike letters I and O as 1 and 0.
    /// </summary>
    public static RoomCode? Find(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        string upper = text.ToUpperInvariant();
        for (int start = 0; start + WireLength <= upper.Length; start++)
        {
            if (upper[start] != 'U' || upper[start + 1] != '/')
                continue;
            if (TryRead(upper, start + 2, out var number))
                return new RoomCode(number);
        }
        return null;
    }

    private static string BuildText(char[] body)
    {
        var text = new StringBuilder(WireLength).Append("U/");
        for (int i = 0; i < BodyDigits; i++)
        {
            if (i > 0 && i % 4 == 0)
                text.Append('-');
            text.Append(body[i]);
        }
        return text.ToString();
    }

    private static string Slice(char[] body, int start) => new(body, start, 4);

    /// <summary>Reads the sixteen digits back into a number, checking the divider along the way.</summary>
    private static bool TryRead(string text, int offset, out BigInteger number)
    {
        number = BigInteger.Zero;
        for (int i = WireLength - 3; i >= 0; i--)
        {
            char c = text[offset + i];
            if (i == 4 || i == 9 || i == 14)
            {
                if (c != '-')
                    return false;
                continue;
            }

            int digit = DigitValue(c);
            if (digit < 0)
                return false;
            number = number * Alphabet.Length + digit;
        }
        return number % CheckDivisor == 0;
    }

    private static int DigitValue(char c)
    {
        if (c == 'I')
            c = '1';
        else if (c == 'O')
            c = '0';
        return Alphabet.IndexOf(c);
    }
}
