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
using SL.Core.Link.Runtime;
using SL.Core.Link.Room;

namespace SL.Core.Link;

/// <summary>Which side of a room this launcher is on.</summary>
public enum LinkRole
{
    None,
    Host,
    Guest,
}

/// <summary>Where a session stands, so the window never has to guess from a stray flag.</summary>
public enum LinkStatus
{
    /// <summary>Nothing is running.</summary>
    Idle,

    /// <summary>Fetching the runtime, starting the virtual network or connecting.</summary>
    Preparing,

    /// <summary>The room exists, but the host has not entered a world yet.</summary>
    WaitingForWorld,

    /// <summary>The room is up and guests may connect.</summary>
    Hosting,

    /// <summary>The guest is in the room.</summary>
    Online,

    /// <summary>The room ended underneath us: kernel exit, host gone, network lost.</summary>
    Disconnected,
}

/// <summary>What the host wants its room to be.</summary>
public sealed record HostRequest(
    string RoomName,
    string Notice,
    int MaxPlayers,
    ModShare? Mods);

/// <summary>
/// One live room session: starts the virtual network, runs the host service or the guest connection, and
/// keeps the player list, the room code and the world port in one place for the window to show.
/// </summary>
public sealed class LinkSession : IAsyncDisposable
{
    private readonly PlayerProfile _me;
    private readonly HashSet<string> _blocked = new(StringComparer.OrdinalIgnoreCase);

    private RoomServer? _server;
    private RoomClient? _client;
    private EasyTierInstance? _runner;
    private RoomCode? _code;
    private RoomInfo _room = new("", "", 0);
    private LinkStatus _status = LinkStatus.Idle;
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

    /// <summary>Lifecycle state of the session.</summary>
    public LinkStatus Status => _status;

    /// <summary>Why the session stopped or failed, when it did.</summary>
    public string? Error { get; private set; }

    /// <summary>The invite code of the room; <c>null</c> before the session comes up.</summary>
    public RoomCode? Code => _code;

    /// <summary>Name, announcement and limit of the room.</summary>
    public RoomInfo Room => _room;

    /// <summary>Port of the game inside the room network; 0 while the host has not entered a world.</summary>
    public int GamePort => _gamePort;

    /// <summary>The player list as of the last update, host first when this launcher is the host.</summary>
    public IReadOnlyList<PlayerProfile> Players => _server?.Players ?? _client?.Players ?? [];

    /// <summary>Whether the room has a world and a port, so a guest may actually play.</summary>
    public bool WorldReady => _gamePort is >= 1 and <= 65535;

    /// <summary>Raised on the thread pool whenever the session changes in a way the window should redraw.</summary>
    public event Action? Changed;

    /// <summary>Lines worth showing in the page log.</summary>
    public event Action<string>? Log;

    /// <summary>Address a guest types into Minecraft's direct connect box.</summary>
    public string JoinAddress => $"{RoomProtocol.HostAddress}:{_gamePort}";

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

    /// <summary>
    /// Opens a room straight away: the code exists before the world does, so the host can share it first and
    /// enter the world afterwards. The world port is bound later through <see cref="BindGamePort"/>.
    /// </summary>
    public static async Task<LinkSession> HostAsync(
        HostRequest request,
        PlayerProfile me,
        IProgress<double>? downloadProgress,
        CancellationToken token)
    {
        var session = new LinkSession(LinkRole.Host, me);
        session._code = RoomCode.Create();
        session._room = new RoomInfo(request.RoomName, request.Notice, request.MaxPlayers);
        session.SetStatus(LinkStatus.Preparing);

        try
        {
            await EnsureRuntimeAsync(downloadProgress, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            // the host name carries the room service port, which is how a guest finds the service
            var config = CreateConfig(session._code, RoomProtocol.HostNamePrefix + RoomServer.ServicePort, RoomProtocol.HostAddress);
            config.TcpWhitelist.Add(RoomServer.ServicePort.ToString());
            session._runner = EasyTierInstance.Start(config);
            session.HookRunner();

            session._server = new RoomServer(
                session._room,
                me,
                gamePort: 0,
                request.Mods is null ? null : request.Mods.PathFor,
                RoomServer.ServicePort,
                session.IsBlocked);
            if (request.Mods is { HasMods: true } mods)
                session._server.Mods = mods.List;
            session._server.PlayersChanged += session.RaiseChanged;
            session._server.Start();

            Current = session;
            session.SetStatus(LinkStatus.WaitingForWorld);
            session.Log?.Invoke($"房间已开:{session._code.Code} · 等待你在游戏里进入世界");
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Joins a room. The virtual network comes up first and the room service is retried until the host has
    /// entered a world, so a guest may paste the code before the host opens anything.
    /// </summary>
    public static async Task<LinkSession> JoinAsync(
        RoomCode code,
        PlayerProfile me,
        IProgress<double>? downloadProgress,
        CancellationToken token)
    {
        var session = new LinkSession(LinkRole.Guest, me);
        session._code = code;
        session.SetStatus(LinkStatus.Preparing);

        try
        {
            await EnsureRuntimeAsync(downloadProgress, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            session.Log?.Invoke("正在加入虚拟局域网…");

            // a guest takes whatever address the network hands out and looks the host up by its name
            var config = CreateConfig(code, "salmon-guest-" + Hex.Random(4), null);
            session._runner = EasyTierInstance.Start(config);
            session.HookRunner();

            var deadline = DateTime.UtcNow.AddSeconds(Math.Max(30, LinkSettings.Current.SearchTimeoutSeconds));
            var endpoint = await FindHostAsync(session._runner, deadline, session.Log, token).ConfigureAwait(false);
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
            session.SetStatus(LinkStatus.Online);
            session.Log?.Invoke($"已加入房间「{session._room.Name}」· 游戏端口 {session._gamePort}");
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Host only: points the room at the world the host just opened. Called whenever the launcher sees a
    /// Minecraft LAN announcement, so the host never has to type a port.
    /// </summary>
    public void BindGamePort(int port, string worldName)
    {
        if (_server is null || port is < 1 or > 65535 || port == _gamePort)
            return;

        _gamePort = port;
        _server.GamePort = port;
        SetStatus(LinkStatus.Hosting);
        Log?.Invoke($"已绑定世界「{worldName}」· 端口 {port}");
        RaiseChanged();
    }

    /// <summary>Takes a player out of the room and remembers it, so the same code cannot walk back in.</summary>
    public void BlockAndKick(string playerId, string playerName)
    {
        if (playerId.Length == 0)
            return;
        _blocked.Add(playerId);
        _server?.Kick(playerId);
        Log?.Invoke($"已移出 {playerName},并禁止其再次加入");
    }

    private bool IsBlocked(string playerId) => _blocked.Contains(playerId);

    private void HookRunner()
    {
        if (_runner is null)
            return;
        _runner.Exited += (_, _) =>
        {
            if (_stopped)
                return;
            string output = _runner?.GetRecentOutput() ?? "";
            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(8))
                Log?.Invoke("[内核] " + line.Trim());
            SetStatus(LinkStatus.Disconnected, "联机内核已退出");
        };
    }

    private void OnRoomClosed(string reason)
    {
        if (_stopped)
            return;
        Log?.Invoke(reason);
        SetStatus(LinkStatus.Disconnected, reason);
    }

    /// <summary>Installs and self-tests the EasyTier component the first time a room is opened.</summary>
    private static async Task EnsureRuntimeAsync(IProgress<double>? progress, CancellationToken token)
    {
        if (!EasyTierPackage.IsInstalled())
        {
            LinkLog.Info("正在安装联机组件 EasyTier " + EasyTierPackage.Version);
            var reporter = new Progress<DownloadProgress>(p => progress?.Report(p.Fraction));
            await EasyTierPackage.InstallAsync(reporter, token).ConfigureAwait(false);
        }

        if (!await EasyTierPackage.CanRunAsync().ConfigureAwait(false))
            throw new LinkException("联机组件无法在当前系统运行,请检查杀毒软件拦截或系统版本");
    }

    /// <summary>Builds the network config both sides share; only the name and address differ.</summary>
    private static EasyTierConfig CreateConfig(RoomCode code, string hostname, string? ipv4)
    {
        var settings = LinkSettings.Current;
        var config = new EasyTierConfig
        {
            NetworkName = code.NetworkName,
            NetworkSecret = code.NetworkSecret,
            Hostname = hostname,
            IPv4 = ipv4 ?? "",
            NoTun = false,   // the room service is addressed by virtual IP, so the adapter must exist
            DirectOnly = !settings.UsePublicNodes,
            HideAddress = settings.HideAddress,
        };
        foreach (string peer in LinkSettings.GetNodes())
            config.Peers.Add(peer);
        return config;
    }

    /// <summary>
    /// Waits for the host to appear in the network. The host puts the room service port at the end of its
    /// name, so its address and port come from one peer entry and no fixed address is assumed.
    /// </summary>
    private static async Task<IPEndPoint> FindHostAsync(
        EasyTierInstance network, DateTime deadline, Action<string>? log, CancellationToken token)
    {
        bool warned = false;
        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var peers = await network.GetPeersAsync(token).ConfigureAwait(false);
                foreach (var peer in peers)
                {
                    if (peer.IsLocal || !peer.Hostname.StartsWith(RoomProtocol.HostNamePrefix, StringComparison.Ordinal))
                        continue;
                    string portText = peer.Hostname[RoomProtocol.HostNamePrefix.Length..];
                    if (int.TryParse(portText, out int port) && port is > 0 and < 65536
                        && IPAddress.TryParse(peer.IPv4, out var address))
                    {
                        log?.Invoke($"已找到房主 {peer.IPv4}:{port}({(peer.IsDirect ? "直连" : "中转")})");
                        return new IPEndPoint(address, port);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (!warned)
                {
                    warned = true;
                    log?.Invoke("读取网络节点失败:" + ex.Message);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
        }

        throw new LinkException("房间无响应,请确认房间码正确、双方网络正常");
    }

    private void SetStatus(LinkStatus status, string? error = null)
    {
        _status = status;
        Error = error;
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
    /// Copies the host's mods into <paramref name="targetDirectory"/>. A file is skipped only when the local
    /// copy has the same size <em>and</em> the same SHA-1, so a truncated or swapped jar is replaced.
    /// Reports 0..1 through <paramref name="progress"/>.
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
            if (Matches(target, mod))
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

            // a truncated or tampered download must never reach the mods folder
            if (!Matches(temporary, mod))
            {
                try { File.Delete(temporary); } catch { }
                throw new LinkException($"模组 {mod.Name} 校验失败,已丢弃");
            }

            File.Move(temporary, target, overwrite: true);
            written++;
        }

        Log?.Invoke($"已同步 {written} 个模组到 {targetDirectory}");
        return written;
    }

    /// <summary>Whether a local file is already exactly the file the host offers.</summary>
    private static bool Matches(string path, ModEntry mod)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != mod.Size)
            return false;
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData(stream)) == mod.Sha1.ToLowerInvariant();
        }
        catch (IOException)
        {
            return false;
        }
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

        try { _runner?.Dispose(); } catch { }

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
        _status = LinkStatus.Idle;
        Error = null;
        Log?.Invoke("联机已停止");
        RaiseChanged();
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    /// <summary>The host's own profile, exposed so the window can highlight the host row.</summary>
    public string MyId => _me.Id;
}
