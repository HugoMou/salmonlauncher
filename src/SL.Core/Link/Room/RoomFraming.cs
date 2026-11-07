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

namespace SL.Core.Link.Room;

/// <summary>
/// Wire framing of the room service.
/// </summary>
/// <remarks>
/// Request:  kind length (1 byte), kind ("namespace:path", UTF-8), body length (4 bytes big-endian), body.
/// Response: status (1 byte: 0 success, 32 not ready, 255 error), body length (4 bytes big-endian), body.
/// </remarks>
public static class RoomFraming
{
    public const byte StatusOk = 0;
    public const byte StatusNotReady = 32;
    public const byte StatusError = 255;

    /// <summary>Largest body accepted in either direction.</summary>
    public const int MaxBodySize = 1024 * 1024;

    public static Task WriteRequestAsync(Stream stream, string kind, ReadOnlyMemory<byte> body, CancellationToken token)
    {
        byte[] kindBytes = Encoding.UTF8.GetBytes(kind);
        if (kindBytes.Length > 255)
            throw new ArgumentException("请求类型过长", nameof(kind));

        var frame = new byte[1 + kindBytes.Length + 4 + body.Length];
        frame[0] = (byte)kindBytes.Length;
        kindBytes.CopyTo(frame, 1);
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(1 + kindBytes.Length, 4), body.Length);
        body.Span.CopyTo(frame.AsSpan(5 + kindBytes.Length));
        return SendAsync(stream, frame, token);
    }

    /// <summary>Reads one request; returns <c>null</c> when the peer closed the connection cleanly.</summary>
    public static async Task<Request?> ReadRequestAsync(Stream stream, CancellationToken token)
    {
        var head = new byte[1];
        if (await stream.ReadAsync(head, token).ConfigureAwait(false) == 0)
            return null;

        byte[] kind = await ReadExactAsync(stream, head[0], token).ConfigureAwait(false);
        byte[] body = await ReadBodyAsync(stream, token).ConfigureAwait(false);
        return new Request(Encoding.UTF8.GetString(kind), body);
    }

    public static Task WriteResponseAsync(Stream stream, byte status, ReadOnlyMemory<byte> body, CancellationToken token)
    {
        var frame = new byte[5 + body.Length];
        frame[0] = status;
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(1, 4), body.Length);
        body.Span.CopyTo(frame.AsSpan(5));
        return SendAsync(stream, frame, token);
    }

    public static async Task<Response> ReadResponseAsync(Stream stream, CancellationToken token)
    {
        byte[] status = await ReadExactAsync(stream, 1, token).ConfigureAwait(false);
        byte[] body = await ReadBodyAsync(stream, token).ConfigureAwait(false);
        return new Response(status[0], body);
    }

    private static async Task SendAsync(Stream stream, byte[] frame, CancellationToken token)
    {
        await stream.WriteAsync(frame, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadBodyAsync(Stream stream, CancellationToken token)
    {
        byte[] size = await ReadExactAsync(stream, 4, token).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32BigEndian(size);
        if (length < 0 || length > MaxBodySize)
            throw new InvalidDataException("消息体过大");
        return await ReadExactAsync(stream, length, token).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int count, CancellationToken token)
    {
        if (count == 0)
            return [];

        var buffer = new byte[count];
        int filled = 0;
        while (filled < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(filled, count - filled), token).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("连接已被关闭");
            filled += read;
        }
        return buffer;
    }

    /// <summary>One decoded request frame.</summary>
    public sealed record Request(string Kind, byte[] Body);

    /// <summary>One decoded response frame.</summary>
    public sealed record Response(byte Status, byte[] Body)
    {
        public bool IsOk => Status == StatusOk;
    }
}
