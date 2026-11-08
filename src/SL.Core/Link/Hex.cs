// ---------------------------------------------------------------------------
// Ported from Campfire (https://github.com/Xocio/Campfire), branch main, 2026-11-08.
// Copyright (C) the Campfire contributors. Licensed under GPL-3.0.
// Adapted for SalmonLauncher: namespace only.
// See THIRD-PARTY-NOTICES.md for the full statement.
// ---------------------------------------------------------------------------
using System.Linq;
using System.Security.Cryptography;

namespace SL.Core.Link
{
    /// <summary>Lowercase hexadecimal text, as used for ids and file hashes.</summary>
    internal static class Hex
    {
        public static string Of(byte[] bytes)
        {
            return string.Concat(bytes.Select(b => b.ToString("x2")));
        }

        /// <summary><paramref name="byteCount"/> cryptographically random bytes as hexadecimal text.</summary>
        public static string Random(int byteCount)
        {
            var bytes = new byte[byteCount];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return Of(bytes);
        }
    }
}
