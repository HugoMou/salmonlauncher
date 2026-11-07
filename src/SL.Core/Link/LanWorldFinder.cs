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
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SL.Core.Link;

/// <summary>A world a player opened to LAN, as announced by the game on this subnet.</summary>
/// <param name="Address">Address of the machine that hosts the world.</param>
/// <param name="Port">Port the game listens on for that world.</param>
/// <param name="Motd">Name the game announced, usually the world or player name.</param>
public sealed record LanWorld(string Address, int Port, string Motd)
{
    public string Key => $"{Address}:{Port}";

    public string Display => $"{Motd} · {Address}:{Port}";
}

/// <summary>
/// Listens to Minecraft's own LAN announcement (<c>224.0.2.60:4445</c>, payload
/// <c>[MOTD]name[/MOTD][AD]port[/AD]</c>), which is what the game broadcasts after "Open to LAN".
/// The host of a room needs this to learn the random port the game picked.
/// </summary>
public sealed class LanWorldFinder : IDisposable
{
    private static readonly IPAddress Group = IPAddress.Parse("224.0.2.60");
    private const int ListenPort = 4445;

    private UdpClient? _udp;
    private CancellationTokenSource? _cts;

    /// <summary>Raised for every announcement (thread-pool thread; marshal to the UI on your own).</summary>
    public event Action<LanWorld>? Found;

    /// <summary>Raised once when listening is impossible (port taken, adapter trouble).</summary>
    public event Action<string>? Failed;

    public bool Running => _udp is not null;

    public void Start()
    {
        if (_udp is not null)
            return;

        var cts = new CancellationTokenSource();
        UdpClient udp;
        try
        {
            udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, ListenPort));
            try
            {
                udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(Group, IPAddress.Any));
            }
            catch (SocketException)
            {
                udp.JoinMulticastGroup(Group);
            }
        }
        catch (Exception ex)
        {
            cts.Dispose();
            Failed?.Invoke("局域网监听失败:" + ex.Message);
            return;
        }

        _udp = udp;
        _cts = cts;
        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = await udp.ReceiveAsync(token).ConfigureAwait(false);
                    var world = Parse(Encoding.UTF8.GetString(result.Buffer), result.RemoteEndPoint.Address);
                    if (world is not null)
                        Found?.Invoke(world);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception)
                {
                    // one malformed packet must not stop the listener
                }
            }
        }, token);
    }

    /// <summary>Reads one announcement; returns <c>null</c> when it carries no usable port.</summary>
    public static LanWorld? Parse(string payload, IPAddress source)
    {
        string? motd = Between(payload, "[MOTD]", "[/MOTD]");
        string? ad = Between(payload, "[AD]", "[/AD]");
        if (ad is null || !int.TryParse(ad, out int port) || port is < 1 or > 65535)
            return null;
        return new LanWorld(source.ToString(), port, string.IsNullOrWhiteSpace(motd) ? "Minecraft 局域网世界" : motd);
    }

    private static string? Between(string text, string open, string close)
    {
        int start = text.IndexOf(open, StringComparison.Ordinal);
        if (start < 0)
            return null;
        start += open.Length;
        int end = text.IndexOf(close, start, StringComparison.Ordinal);
        return end < 0 ? null : text[start..end];
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        _cts?.Dispose();
        _cts = null;
        try { _udp?.Dispose(); } catch { }
        _udp = null;
    }
}
