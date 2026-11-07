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
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SL.Core.Link.Room;

/// <summary>
/// The guest half of a room: connects to a host inside the room network, learns the game port and keeps the
/// player list up to date with a heartbeat. One instance per joined room.
/// </summary>
public sealed class RoomClient : IAsyncDisposable
{
    private static readonly byte[] PingBody = Encoding.UTF8.GetBytes("salmon-link");

    /// <summary>How often the guest says it is still there, and how often it refreshes the player list.</summary>
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly PlayerProfile _me;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _cts = new();

    private List<PlayerProfile> _players = [];
    private RoomInfo _room = new("", "", 0);
    private int _gamePort;
    private Task? _heartbeat;
    private bool _closed;

    private RoomClient(TcpClient client, NetworkStream stream, PlayerProfile me)
    {
        _client = client;
        _stream = stream;
        _me = me;
    }

    /// <summary>What the host calls its room.</summary>
    public RoomInfo Room => _room;

    /// <summary>Port of the host's game inside the room network.</summary>
    public int GamePort => _gamePort;

    /// <summary>Latest player list the host reported.</summary>
    public IReadOnlyList<PlayerProfile> Players
    {
        get
        {
            lock (_players)
                return _players.ToArray();
        }
    }

    /// <summary>Raised when the player list changes (thread-pool thread).</summary>
    public event Action? PlayersChanged;

    /// <summary>Raised once when the room ends, with a line that can be shown to the player as-is.</summary>
    public event Action<string>? Closed;

    /// <summary>
    /// Connects to a host room service and completes the handshake.
    /// Throws <see cref="RoomRejectedException"/> when the host refuses, <see cref="LinkException"/> otherwise.
    /// </summary>
    public static async Task<RoomClient> ConnectAsync(IPEndPoint endpoint, PlayerProfile me, CancellationToken token)
    {
        var client = new TcpClient { NoDelay = true };
        try
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(8));
                await client.ConnectAsync(endpoint.Address, endpoint.Port, timeout.Token).ConfigureAwait(false);
            }

            var stream = client.GetStream();
            var roomClient = new RoomClient(client, stream, me);
            try
            {
                await roomClient.HandshakeAsync(token).ConfigureAwait(false);
            }
            catch
            {
                await roomClient.DisposeAsync().ConfigureAwait(false);
                throw;
            }
            return roomClient;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            try { client.Dispose(); } catch { }
            throw new LinkException("无法连接到房主,请确认房间码正确且双方网络可达", ex);
        }
        catch
        {
            try { client.Dispose(); } catch { }
            throw;
        }
    }

    /// <summary>Walks the handshake: verify the fingerprint, check protocols, check in, read port and room.</summary>
    private async Task HandshakeAsync(CancellationToken token)
    {
        var pong = await RequestAsync(RoomProtocol.Ping, PingBody, token).ConfigureAwait(false);
        if (!pong.AsSpan().SequenceEqual(PingBody) || !await LooksLikeRoomAsync(token).ConfigureAwait(false))
            throw new LinkException("对方不是 Salmon 房间服务");

        var protocols = RoomMessages.ReadProtocolList(await RequestAsync(RoomProtocol.Protocols, [], token).ConfigureAwait(false));
        if (!protocols.Contains(RoomProtocol.ServerPort))
            throw new LinkException("房主版本过旧,不支持获取游戏端口");

        // checking in may be refused before we know anything else about the room
        await RequestAsync(RoomProtocol.PlayerPing, _me.ToJson(), token, allowRejection: true).ConfigureAwait(false);

        int port = RoomMessages.ReadServerPort(await RequestAsync(RoomProtocol.ServerPort, [], token).ConfigureAwait(false));
        if (port is < 1 or > 65535)
            throw new LinkException("房主没有发布游戏端口");

        var roomJson = await RequestAsync(RoomProtocol.Room, [], token).ConfigureAwait(false);
        RoomInfo room = new("", "", 0);
        using (var document = System.Text.Json.JsonDocument.Parse(roomJson))
        {
            room = RoomInfo.FromJson(document.RootElement) ?? room;
        }

        SetRoom(room, port);
        await RefreshPlayersAsync(token).ConfigureAwait(false);
        _heartbeat = Task.Run(() => HeartbeatLoopAsync(_cts.Token));
    }

    /// <summary>Asks the host to echo the fixed marker, which a plain port scanner cannot fake.</summary>
    private async Task<bool> LooksLikeRoomAsync(CancellationToken token)
    {
        var echo = await RequestAsync(RoomProtocol.Ping, RoomProtocol.Fingerprint, token).ConfigureAwait(false);
        return echo.AsSpan().SequenceEqual(RoomProtocol.Fingerprint);
    }

    // the handshake fills the room in after the connection exists, so both stay behind fields
    private void SetRoom(RoomInfo room, int port)
    {
        _room = room;
        _gamePort = port;
    }

    /// <summary>Refreshes the player list; a failure here is not fatal, the next heartbeat retries.</summary>
    private async Task RefreshPlayersAsync(CancellationToken token)
    {
        try
        {
            var json = await RequestAsync(RoomProtocol.PlayerList, [], token).ConfigureAwait(false);
            var players = RoomMessages.ReadPlayerList(json);
            lock (_players)
                _players = players;
            PlayersChanged?.Invoke();
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            // the heartbeat assumes a bad list is transient
        }
    }

    /// <summary>Mods the host offers, or an empty list when the host does not share any.</summary>
    public async Task<ModList> GetModsAsync(CancellationToken token = default)
    {
        try
        {
            return ModList.FromJson(await RequestAsync(RoomProtocol.ModList, [], token).ConfigureAwait(false));
        }
        catch (Exception) when (!_closed)
        {
            return ModList.Empty;
        }
    }

    /// <summary>Reads one slice of a host mod; an empty answer means the end of the file was reached.</summary>
    public async Task<byte[]> ReadModChunkAsync(string sha1, long offset, CancellationToken token = default)
    {
        var body = new byte[48];
        Encoding.ASCII.GetBytes(sha1.ToLowerInvariant(), body.AsSpan(0, 40));
        BinaryPrimitives.WriteInt64BigEndian(body.AsSpan(40, 8), offset);
        return await RequestAsync(RoomProtocol.ModFile, body, token).ConfigureAwait(false);
    }

    /// <summary>Sends the heartbeat; when the host stops answering the room is reported as closed.</summary>
    private async Task HeartbeatLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(HeartbeatInterval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await RequestAsync(RoomProtocol.PlayerPing, _me.ToJson(), token).ConfigureAwait(false);
                await RefreshPlayersAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!_closed)
                {
                    _closed = true;
                    Closed?.Invoke("与房主的连接已断开:" + ex.Message);
                }
                return;
            }
        }
    }

    /// <summary>
    /// One request/response round trip. The room service allows a single exchange at a time per connection,
    /// so every send is serialised. <paramref name="allowRejection"/> turns a host refusal into a
    /// <see cref="RoomRejectedException"/> instead of a generic error.
    /// </summary>
    private async Task<byte[]> RequestAsync(string kind, byte[] body, CancellationToken token, bool allowRejection = false)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await RoomFraming.WriteRequestAsync(_stream, kind, body, token).ConfigureAwait(false);
            var response = await RoomFraming.ReadResponseAsync(_stream, token).ConfigureAwait(false);
            if (response.IsOk)
                return response.Body;

            // "not ready" is the host saying the code is right but no world is open yet; retry, do not fail
            if (response.Status == RoomFraming.StatusNotReady)
                throw new LinkException("房主还没有进入世界");

            string reason = Encoding.UTF8.GetString(response.Body);
            if (allowRejection)
            {
                if (reason == RoomMessages.Rejection.Full)
                    throw new RoomRejectedException(blocked: false, full: true);
                if (reason == RoomMessages.Rejection.Blocked || reason == RoomMessages.Rejection.Removed)
                    throw new RoomRejectedException(blocked: true);
            }
            throw new LinkException(string.IsNullOrWhiteSpace(reason) ? "房主返回了错误" : reason);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _closed = true;
        try { _cts.Cancel(); } catch { }

        if (_heartbeat is not null)
        {
            try { await _heartbeat.ConfigureAwait(false); } catch { }
        }

        try { await _stream.DisposeAsync().ConfigureAwait(false); } catch { }
        try { _client.Dispose(); } catch { }
        _gate.Dispose();
        _cts.Dispose();
    }
}
