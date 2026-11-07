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
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SL.Core.Link.Room;

/// <summary>
/// The host half of a room: one TCP service on the room's virtual network that tells guests where the game
/// listens, keeps the player list and hands out the host's mods. Guests hold one long-lived connection each.
/// </summary>
public sealed class RoomServer : IAsyncDisposable
{
    /// <summary>Port the room service always uses, so a guest never has to be told a second number.</summary>
    public const int ServicePort = 38471;

    /// <summary>A guest that has not said anything for this long drops out of the player list.</summary>
    private const int HeartbeatTimeoutSeconds = 30;

    /// <summary>Largest slice of a mod file sent in one response.</summary>
    private const int ModChunkSize = 512 * 1024;

    private readonly RoomInfo _info;
    private readonly PlayerProfile _host;
    private readonly int _requestedPort;
    private readonly Func<string, string?>? _modPath;
    private readonly Func<string, bool>? _isBlocked;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _gate = new();
    private readonly List<PlayerProfile> _players = [];
    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.OrdinalIgnoreCase);

    private TcpListener? _listener;
    private Task? _acceptLoop;
    private Task? _reaperLoop;

    /// <param name="info">Room name, announcement and player limit.</param>
    /// <param name="host">The host itself, already carrying its name and account kind.</param>
    /// <param name="gamePort">Port the host's game listens on inside the room network.</param>
    /// <param name="modPath">Maps a mod's SHA-1 to its file on disk; guests that ask for it get the bytes.</param>
    /// <param name="port">Port to listen on; 0 lets the system pick one (used by tests).</param>
    /// <param name="isBlocked">Players the host removed and does not want back.</param>
    public RoomServer(
        RoomInfo info,
        PlayerProfile host,
        int gamePort,
        Func<string, string?>? modPath = null,
        int port = ServicePort,
        Func<string, bool>? isBlocked = null)
    {
        _info = info;
        _host = host.AsHost();
        GamePort = gamePort;
        _modPath = modPath;
        _isBlocked = isBlocked;
        _requestedPort = port;
        _players.Add(_host);
    }

    /// <summary>The port actually bound.</summary>
    public int Port { get; private set; }

    /// <summary>Port of the host's game inside the room network; guests connect here.</summary>
    public int GamePort { get; set; }

    /// <summary>Mods offered to guests; empty when the host does not share any.</summary>
    public ModList Mods { get; set; } = ModList.Empty;

    /// <summary>Voice chat endpoint offered to guests; <see cref="VoiceInfo.Off"/> when the host has none.</summary>
    public VoiceInfo Voice { get; set; } = VoiceInfo.Off;

    /// <summary>The player limit of the room; 0 means unlimited.</summary>
    public int MaxPlayers => _info.MaxPlayers;

    /// <summary>Host first, then every guest that checked in recently.</summary>
    public IReadOnlyList<PlayerProfile> Players
    {
        get
        {
            lock (_gate)
                return _players.ToArray();
        }
    }

    /// <summary>Raised whenever the player list changes (thread-pool thread).</summary>
    public event Action? PlayersChanged;

    /// <summary>Binds the service and starts accepting guests.</summary>
    public void Start()
    {
        if (_listener is not null)
            return;

        var listener = new TcpListener(IPAddress.Any, _requestedPort);
        try
        {
            listener.Start();
        }
        catch (SocketException ex)
        {
            throw new LinkException($"联机服务端口 {_requestedPort} 被占用,请关闭占用该端口的程序后重试", ex);
        }
        _listener = listener;
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        _reaperLoop = Task.Run(() => ReaperLoopAsync(_cts.Token));
        LinkLog.Info($"房间服务已监听 {Port},游戏端口 {GamePort}");
    }

    /// <summary>Takes a guest out of the room and closes its connection.</summary>
    public void Kick(string playerId)
    {
        if (_sessions.TryRemove(playerId, out var session))
        {
            try { session.Client?.Close(); } catch { }
            RemovePlayer(playerId);
            LinkLog.Info($"已移出玩家 {playerId}");
        }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                continue;
            }

            _ = Task.Run(() => ServeAsync(client, token), token);
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken token)
    {
        string remote = client.Client.RemoteEndPoint?.ToString() ?? "?";
        Session? session = null;
        try
        {
            client.NoDelay = true;
            await using var stream = client.GetStream();
            while (!token.IsCancellationRequested)
            {
                var request = await RoomFraming.ReadRequestAsync(stream, token).ConfigureAwait(false);
                if (request is null)
                    break;

                // a guest must check in before it can see anything about the room, but the two probes that only
                // prove "this is a room service" are answered first so the handshake can identify itself
                if (session is null)
                {
                    if (request.Kind is RoomProtocol.Ping or RoomProtocol.Protocols)
                    {
                        var (probeStatus, probeBody) = Handle(request, remote);
                        await RoomFraming.WriteResponseAsync(stream, probeStatus, probeBody, token).ConfigureAwait(false);
                        continue;
                    }

                    if (request.Kind != RoomProtocol.PlayerPing)
                    {
                        await RejectAsync(stream, RoomMessages.Rejection.Removed, token).ConfigureAwait(false);
                        return;
                    }

                    var admission = Admit(request.Body, remote, client);
                    if (admission.Rejection is not null)
                    {
                        await RejectAsync(stream, admission.Rejection, token).ConfigureAwait(false);
                        return;
                    }

                    session = admission.Session!;
                    _sessions[session.PlayerId] = session;
                    await RoomFraming.WriteResponseAsync(stream, RoomFraming.StatusOk, ReadOnlyMemory<byte>.Empty, token).ConfigureAwait(false);
                    continue;
                }

                session.LastSeen = DateTime.UtcNow;

                if (request.Kind == RoomProtocol.PlayerPing)
                {
                    // a guest may rename itself, but the id it checked in with never changes
                    var profile = ParseProfile(request.Body);
                    if (profile is null || !string.Equals(profile.Id, session.PlayerId, StringComparison.OrdinalIgnoreCase))
                    {
                        await RejectAsync(stream, RoomMessages.Rejection.Removed, token).ConfigureAwait(false);
                        return;
                    }

                    UpsertPlayer(profile);
                    await RoomFraming.WriteResponseAsync(stream, RoomFraming.StatusOk, ReadOnlyMemory<byte>.Empty, token).ConfigureAwait(false);
                    continue;
                }

                var (status, body) = Handle(request, remote);
                await RoomFraming.WriteResponseAsync(stream, status, body, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            LinkLog.Warn($"房间连接 {remote} 结束:{ex.Message}");
        }
        finally
        {
            try { client.Dispose(); } catch { }
            if (session is not null)
            {
                _sessions.TryRemove(session.PlayerId, out _);
                RemovePlayer(session.PlayerId);
            }
        }
    }

    private static Task RejectAsync(Stream stream, string reason, CancellationToken token)
        => RoomFraming.WriteResponseAsync(stream, RoomFraming.StatusError, Encoding.UTF8.GetBytes(reason), token);

    /// <summary>Validates a heartbeat and adds the player; <see cref="Admission.Rejection"/> says why when refused.</summary>
    private Admission Admit(byte[] body, string remote, TcpClient client)
    {
        var profile = ParseProfile(body);
        if (profile is null)
            return new Admission(null, RoomMessages.Rejection.Removed);

        if (_isBlocked?.Invoke(profile.Id) == true)
        {
            LinkLog.Info($"拒绝已被移出的玩家 {profile.Name}({remote})");
            return new Admission(null, RoomMessages.Rejection.Blocked);
        }

        bool known = _sessions.ContainsKey(profile.Id)
                     || string.Equals(profile.Id, _host.Id, StringComparison.OrdinalIgnoreCase);
        if (!known && IsFull())
        {
            LinkLog.Warn($"房间已满,拒绝 {profile.Name}({remote})");
            return new Admission(null, RoomMessages.Rejection.Full);
        }

        UpsertPlayer(profile);
        if (!known)
            LinkLog.Info($"{profile.Name} 加入了房间({remote})");

        return new Admission(new Session(profile.Id, client), null);
    }

    /// <summary>Reads a heartbeat body into a usable profile; <c>null</c> when it is not one.</summary>
    private static PlayerProfile? ParseProfile(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var profile = PlayerProfile.FromJson(document.RootElement, forcedId: null);
            if (profile is null || !LinkUtil.IsHex(profile.Id, 32) || profile.Name.Length == 0)
                return null;
            return profile;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private bool IsFull()
    {
        if (_info.MaxPlayers <= 0)
            return false;
        lock (_gate)
        {
            // the host counts towards the limit
            return _players.Count >= _info.MaxPlayers;
        }
    }

    /// <summary>Handles one request that needs no room membership check.</summary>
    private (byte Status, byte[] Body) Handle(RoomFraming.Request request, string remote)
    {
        switch (request.Kind)
        {
            case RoomProtocol.Ping:
                return (RoomFraming.StatusOk, request.Body);

            case RoomProtocol.Protocols:
                return (RoomFraming.StatusOk, RoomMessages.ProtocolList(SupportedKinds()));

            case RoomProtocol.ServerPort:
                // the code may be shared before the host has entered a world: say "not yet" instead of failing
                return GamePort > 0
                    ? (RoomFraming.StatusOk, RoomMessages.ServerPort(GamePort))
                    : (RoomFraming.StatusNotReady, Array.Empty<byte>());

            case RoomProtocol.PlayerList:
                return (RoomFraming.StatusOk, RoomMessages.PlayerList(Players));

            case RoomProtocol.Room:
                return (RoomFraming.StatusOk, _info.ToJson());

            case RoomProtocol.ModList:
                return (RoomFraming.StatusOk, Mods.ToJson());

            case RoomProtocol.Voice:
                return (RoomFraming.StatusOk, Voice.ToJson());

            case RoomProtocol.ModFile:
                return ReadModChunk(request.Body);

            default:
                LinkLog.Warn($"{remote} 请求了不支持的类型 {request.Kind}");
                return (RoomFraming.StatusError, Encoding.UTF8.GetBytes("unknown"));
        }
    }

    private IReadOnlyList<string> SupportedKinds()
    {
        var kinds = new List<string>
        {
            RoomProtocol.Ping,
            RoomProtocol.Protocols,
            RoomProtocol.ServerPort,
            RoomProtocol.PlayerPing,
            RoomProtocol.PlayerList,
            RoomProtocol.Room,
        };
        if (Mods.Mods.Count > 0 && _modPath is not null)
        {
            kinds.Add(RoomProtocol.ModList);
            kinds.Add(RoomProtocol.ModFile);
        }
        return kinds;
    }

    /// <summary>Body: the mod's SHA-1 in ASCII hex (40 bytes) followed by the offset as eight big-endian bytes.</summary>
    private (byte Status, byte[] Body) ReadModChunk(byte[] body)
    {
        if (_modPath is null || body.Length != 48)
            return (RoomFraming.StatusError, Encoding.UTF8.GetBytes("bad request"));

        string sha1 = Encoding.ASCII.GetString(body, 0, 40).ToLowerInvariant();
        long offset = BinaryPrimitives.ReadInt64BigEndian(body.AsSpan(40, 8));
        if (!LinkUtil.IsHex(sha1, 40) || offset < 0)
            return (RoomFraming.StatusError, Encoding.UTF8.GetBytes("bad request"));

        string? path = _modPath(sha1);
        if (path is null || !File.Exists(path))
            return (RoomFraming.StatusError, Encoding.UTF8.GetBytes("not found"));

        try
        {
            using var file = File.OpenRead(path);
            if (offset >= file.Length)
                return (RoomFraming.StatusOk, []);

            int count = (int)Math.Min(ModChunkSize, file.Length - offset);
            var buffer = new byte[count];
            file.Seek(offset, SeekOrigin.Begin);
            int read = file.Read(buffer, 0, count);
            return (RoomFraming.StatusOk, read == count ? buffer : buffer[..read]);
        }
        catch (IOException ex)
        {
            LinkLog.Warn("读取模组失败:" + ex.Message);
            return (RoomFraming.StatusError, Encoding.UTF8.GetBytes("read failed"));
        }
    }

    private void UpsertPlayer(PlayerProfile profile)
    {
        lock (_gate)
        {
            int index = _players.FindIndex(p => string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                _players[index] = profile;
            else
                _players.Add(profile);
        }
        PlayersChanged?.Invoke();
    }

    private void RemovePlayer(string playerId)
    {
        bool removed;
        lock (_gate)
            removed = _players.RemoveAll(p => string.Equals(p.Id, playerId, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed)
            PlayersChanged?.Invoke();
    }

    /// <summary>Drops guests whose connection died without a clean shutdown.</summary>
    private async Task ReaperLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var deadline = DateTime.UtcNow.AddSeconds(-HeartbeatTimeoutSeconds);
            foreach (var (id, session) in _sessions)
            {
                if (session.LastSeen < deadline && _sessions.TryRemove(id, out _))
                {
                    try { session.Client?.Close(); } catch { }
                    RemovePlayer(id);
                }
            }
        }
    }

    /// <summary>Stops the service and drops every guest.</summary>
    public async ValueTask DisposeAsync()
    {
        try { _cts.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }

        foreach (var session in _sessions.Values)
        {
            try { session.Client?.Close(); } catch { }
        }
        _sessions.Clear();

        if (_acceptLoop is not null)
        {
            try { await _acceptLoop.ConfigureAwait(false); } catch { }
        }
        if (_reaperLoop is not null)
        {
            try { await _reaperLoop.ConfigureAwait(false); } catch { }
        }
        _cts.Dispose();
        _listener = null;
    }

    /// <summary>Result of a heartbeat: either a session to keep or the reason the player was refused.</summary>
    private readonly record struct Admission(Session? Session, string? Rejection);

    /// <summary>One checked-in guest; the socket lets a kick close the connection.</summary>
    private sealed class Session(string playerId, TcpClient? client)
    {
        public string PlayerId { get; } = playerId;
        public TcpClient? Client { get; } = client;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    }
}
