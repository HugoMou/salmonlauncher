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
using SL.Core.Link.EasyTier;
using SL.Core.Link.Room;

namespace SL.Core.Link;

/// <summary>Which side of a room this launcher is on.</summary>
public enum LinkRole
{
    None,
    Host,
    Guest,
}

/// <summary>What the host wants its room to be.</summary>
public sealed record HostRequest(
    int GamePort,
    string RoomName,
    string Notice,
    int MaxPlayers,
    ModShare? Mods);

/// <summary>
/// One live room session: starts the virtual network, runs the host service or the guest connection, and
/// keeps the player list and the room code in one place for the window to show.
/// </summary>
public sealed class LinkSession : IAsyncDisposable
{
    private readonly PlayerProfile _me;

    private RoomServer? _server;
    private RoomClient? _client;
    private EasyTierRunner? _runner;
    private RoomCode? _code;
    private RoomInfo _room = new("", "", 0);
    private int _gamePort;
    private bool _stopped;

    private LinkSession(LinkRole role, PlayerProfile me)
    {
        Role = role;
        _me = me;
    }

    /// <summary>The session currently in progress, if any.</summary>
    public static LinkSession? Current { get; private set; }

    public LinkRole Role { get; }

    /// <summary>The invite code of the room; <c>null</c> before the session comes up.</summary>
    public RoomCode? Code => _code;

    /// <summary>Name, announcement and limit of the room.</summary>
    public RoomInfo Room => _room;

    /// <summary>Port the game listens on inside the room network.</summary>
    public int GamePort => _gamePort;

    /// <summary>The player list as of the last update, host first when this launcher is the host.</summary>
    public IReadOnlyList<PlayerProfile> Players => _server?.Players ?? _client?.Players ?? [];

    /// <summary>Raised on the thread pool whenever the session changes in a way the window should redraw.</summary>
    public event Action? Changed;

    /// <summary>Lines worth showing in the page log.</summary>
    public event Action<string>? Log;

    /// <summary>Address a guest types into Minecraft's direct connect box.</summary>
    public string JoinAddress => $"{EasyTierArguments.HostAddress}:{_gamePort}";

    /// <summary>Whether this process can build a virtual network at all, and why not when it cannot.</summary>
    public static bool CanRun(out string reason)
    {
        if (!OperatingSystem.IsWindows())
        {
            reason = "联机功能目前只在 Windows 上提供";
            return false;
        }

        // EasyTier needs to create a TUN adapter, which Windows only allows to elevated processes
        if (!Environment.IsPrivilegedProcess)
        {
            reason = "建立虚拟局域网需要管理员权限,请以管理员身份重新启动启动器";
            return false;
        }

        reason = "";
        return true;
    }

    /// <summary>Creates a room: draws a code, starts the virtual network, then serves the game port.</summary>
    public static async Task<LinkSession> HostAsync(
        HostRequest request,
        PlayerProfile me,
        IProgress<double>? downloadProgress,
        CancellationToken token)
    {
        var session = new LinkSession(LinkRole.Host, me);
        var code = RoomCode.Create();
        session._code = code;
        session._room = new RoomInfo(request.RoomName, request.Notice, request.MaxPlayers);
        session._gamePort = request.GamePort;

        try
        {
            string core = await EasyTierRuntime.EnsureAsync(downloadProgress, token).ConfigureAwait(false);
            session._runner = EasyTierRunner.Start(core,
                EasyTierArguments.Host(code, RoomServer.ServicePort, LinkSettings.GetNodes()));
            session.HookRunner();

            session._server = new RoomServer(
                session._room,
                me,
                request.GamePort,
                request.Mods is null ? null : request.Mods.PathFor,
                RoomServer.ServicePort);
            if (request.Mods is { HasMods: true } mods)
                session._server.Mods = mods.List;
            session._server.PlayersChanged += session.RaiseChanged;
            session._server.Start();

            Current = session;
            session.Log?.Invoke($"房间已开:{code.Code} · 游戏端口 {request.GamePort} · 联机服务 {RoomServer.ServicePort}");
            session.RaiseChanged();
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Joins a room: starts the virtual network, waits for the host, then completes the handshake.</summary>
    public static async Task<LinkSession> JoinAsync(
        RoomCode code,
        PlayerProfile me,
        IProgress<double>? downloadProgress,
        CancellationToken token)
    {
        var session = new LinkSession(LinkRole.Guest, me);
        session._code = code;

        try
        {
            string core = await EasyTierRuntime.EnsureAsync(downloadProgress, token).ConfigureAwait(false);
            session.Log?.Invoke("正在加入虚拟局域网…");
            session._runner = EasyTierRunner.Start(core, EasyTierArguments.Guest(code, LinkSettings.GetNodes()));
            session.HookRunner();

            var endpoint = new IPEndPoint(IPAddress.Parse(EasyTierArguments.HostAddress), RoomServer.ServicePort);
            var deadline = DateTime.UtcNow.AddSeconds(Math.Max(30, LinkSettings.Current.SearchTimeoutSeconds));
            RoomClient? client = null;
            Exception? lastError = null;

            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    client = await RoomClient.ConnectAsync(endpoint, me, token).ConfigureAwait(false);
                    break;
                }
                catch (RoomRejectedException)
                {
                    throw;
                }
                catch (LinkException ex)
                {
                    lastError = ex;
                    session.Log?.Invoke("房主尚未响应,继续等待…");
                }

                await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
            }

            if (client is null)
                throw new LinkException("房间无响应,请确认房间码正确、双方网络正常", lastError);

            session._client = client;
            session._room = client.Room;
            session._gamePort = client.GamePort;
            client.PlayersChanged += session.RaiseChanged;
            client.Closed += session.OnRoomClosed;

            Current = session;
            session.Log?.Invoke($"已加入房间「{session._room.Name}」· 游戏端口 {session._gamePort}");
            session.RaiseChanged();
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private void HookRunner()
    {
        if (_runner is null)
            return;
        _runner.Line += line => Log?.Invoke("[内核] " + line);
        _runner.Exited += code =>
        {
            if (_stopped)
                return;
            Log?.Invoke($"[内核] 联机内核已退出({code})");
            RaiseChanged();
        };
    }

    private void OnRoomClosed(string reason)
    {
        if (_stopped)
            return;
        Log?.Invoke(reason);
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        try { Changed?.Invoke(); } catch { }
    }

    /// <summary>Asks the host which mods the room runs.</summary>
    public Task<ModList> FetchModsAsync(CancellationToken token = default)
        => _client is null ? Task.FromResult(ModList.Empty) : _client.GetModsAsync(token);

    /// <summary>
    /// Copies the host's mods into <paramref name="targetDirectory"/>, skipping files that are already there
    /// with the right size. Reports 0..1 through <paramref name="progress"/>.
    /// </summary>
    public async Task<int> DownloadModsAsync(string targetDirectory, IProgress<double>? progress, CancellationToken token)
    {
        if (_client is null)
            throw new LinkException("只有加入房间后才能下载房主的模组");

        var mods = await _client.GetModsAsync(token).ConfigureAwait(false);
        if (mods.Mods.Count == 0)
            return 0;

        Directory.CreateDirectory(targetDirectory);
        long totalBytes = mods.Mods.Sum(m => Math.Max(0, m.Size));
        long copied = 0;
        int written = 0;

        foreach (var mod in mods.Mods)
        {
            token.ThrowIfCancellationRequested();
            string target = Path.Combine(targetDirectory, Path.GetFileName(mod.Name));
            var existing = new FileInfo(target);
            if (existing.Exists && existing.Length == mod.Size)
            {
                copied += mod.Size;
                progress?.Report(totalBytes == 0 ? 1 : (double)copied / totalBytes);
                continue;
            }

            string temporary = target + ".download";
            await using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                long offset = 0;
                while (true)
                {
                    var chunk = await _client.ReadModChunkAsync(mod.Sha1, offset, token).ConfigureAwait(false);
                    if (chunk.Length == 0)
                        break;
                    await file.WriteAsync(chunk, token).ConfigureAwait(false);
                    offset += chunk.Length;
                    copied += chunk.Length;
                    progress?.Report(totalBytes == 0 ? 1 : (double)copied / totalBytes);
                }
            }

            File.Move(temporary, target, overwrite: true);
            written++;
        }

        Log?.Invoke($"已同步 {written} 个模组到 {targetDirectory}");
        return written;
    }

    /// <summary>Ends the session: drops the guests, closes the room service and stops the virtual network.</summary>
    public async Task StopAsync()
    {
        if (_stopped)
            return;
        _stopped = true;

        if (Current == this)
            Current = null;

        if (_client is not null)
        {
            _client.PlayersChanged -= RaiseChanged;
            _client.Closed -= OnRoomClosed;
        }

        if (_server is not null)
            _server.PlayersChanged -= RaiseChanged;

        try { _runner?.Stop(); } catch { }

        if (_client is not null)
        {
            try { await _client.DisposeAsync().ConfigureAwait(false); } catch { }
            _client = null;
        }
        if (_server is not null)
        {
            try { await _server.DisposeAsync().ConfigureAwait(false); } catch { }
            _server = null;
        }

        _runner?.Dispose();
        _runner = null;
        Log?.Invoke("联机已停止");
        RaiseChanged();
    }

    /// <summary>Removes a guest from the room (host only).</summary>
    public void Kick(string playerId) => _server?.Kick(playerId);

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    /// <summary>The host's own profile, exposed so the window can highlight the host row.</summary>
    public string MyId => _me.Id;
}
