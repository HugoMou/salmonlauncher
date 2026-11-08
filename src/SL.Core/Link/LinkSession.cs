// SalmonLauncher (SL) — a Minecraft Java edition launcher
// Copyright (C) 2026 Hugo Mou
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, either version 3 of the License, or (at your option) any later version.
//
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using SL.Core.Link.Mods;
using SL.Core.Link.Room;
using SL.Core.Link.Runtime;

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
    ModSource? Mods);

/// <summary>The mods an instance offers, plus the way back from a hash to the file on disk.</summary>
public sealed class ModSource
{
    private readonly Dictionary<string, string> _pathBySha1 = new(StringComparer.OrdinalIgnoreCase);

    private ModSource(ModList list, Dictionary<string, string> pathBySha1)
    {
        List = list;
        _pathBySha1 = pathBySha1;
    }

    public ModList List { get; }

    public bool HasMods => List.Files.Count > 0;

    /// <summary>Fingerprints every jar in a folder; <c>null</c> when the folder yields nothing.</summary>
    public static ModSource? FromDirectory(string? directory, string gameVersion, string loader)
    {
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var list = LinkSession.ScanMods(directory ?? "", gameVersion, loader, paths);
        return list.Files.Count == 0 ? null : new ModSource(list, paths);
    }

    public string? PathFor(string sha1) => _pathBySha1.TryGetValue(sha1, out string? path) ? path : null;
}

/// <summary>
/// One live room session. The virtual network carries the room service through a local port forward, so the
/// game only ever talks to this machine: no virtual adapter, no administrator rights, no port forwarding on
/// the router. The room service itself is the one ported from Campfire.
/// </summary>
public sealed class LinkSession : IAsyncDisposable
{
    private readonly PlayerProfile _me;

    private RoomServer? _server;
    private RoomClient? _client;
    private EasyTierInstance? _network;
    private RoomCode? _code;
    private RoomInfo _room = new(null, null, 0);
    private LinkStatus _status = LinkStatus.Idle;
    private int _gamePort;
    private int _localGamePort;
    private int _localServicePort;
    private CancellationTokenSource? _heartbeat;
    private bool _stopped;

    private LinkSession(LinkRole role, PlayerProfile me)
    {
        Role = role;
        _me = me;
    }

    /// <summary>The session currently in progress, if any.</summary>
    public static LinkSession? Current { get; private set; }

    public LinkRole Role { get; }

    public LinkStatus Status => _status;

    /// <summary>Why the session stopped or failed, when it did.</summary>
    public string? Error { get; private set; }

    public RoomCode? Code => _code;

    public RoomInfo Room => _room;

    /// <summary>Port of the game inside the room; 0 while the host has not entered a world.</summary>
    public int GamePort => _gamePort;

    /// <summary>Host first, then every guest that checked in recently.</summary>
    public IReadOnlyList<PlayerProfile> Players => _server?.GetPlayers() ?? _clientPlayers;

    private IReadOnlyList<PlayerProfile> _clientPlayers = [];

    /// <summary>Whether the room has a world and a port, so a guest may actually play.</summary>
    public bool WorldReady => _gamePort is >= 1 and <= 65535;

    /// <summary>Raised on the thread pool whenever the session changes in a way the window should redraw.</summary>
    public event Action? Changed;

    /// <summary>Lines worth showing in the page log.</summary>
    public event Action<string>? Log;

    /// <summary>
    /// Address the game connects to: on this machine, because the network forwards the host's game port here.
    /// </summary>
    public string JoinAddress => Role == LinkRole.Host
        ? $"{RoomProtocol.HostAddress}:{_gamePort}"
        : $"127.0.0.1:{_localGamePort}";

    /// <summary>Whether this process can build a room; forwarding means no adapter and no elevation.</summary>
    public static bool CanRun(out string reason)
    {
        if (!OperatingSystem.IsWindows())
        {
            reason = "联机功能目前只在 Windows 上提供";
            return false;
        }

        if (!EasyTierPackage.IsSupportedSystem)
        {
            reason = "当前系统版本过低,联机组件无法运行";
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

            // the advertised port is the room service's own port; guests forward to it
            session._server = RoomServer.Start(me.WithKind(PlayerKind.Host), 0);
            session._server.Room = session._room;
            session._server.ModsAllowed = LinkSettings.Current.ShareMods;
            if (request.Mods is { } mods)
                session._server.Mods = new ModHost(mods.List, mods.PathFor);
            session._server.PlayersChanged += session.OnPlayersChanged;

            var config = CreateConfig(session._code, RoomProtocol.HostNamePrefix + session._server.Port, null);
            session._network = EasyTierInstance.Start(config);
            session.HookNetwork();

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

    /// <summary>Fingerprints every jar in a folder into a mod list, remembering where each hash lives.</summary>
    public static ModList ScanMods(string directory, string gameVersion, string loader, Dictionary<string, string>? paths = null)
    {
        var files = new List<ModFile>();
        if (Directory.Exists(directory))
        {
            foreach (string path in Directory.EnumerateFiles(directory, "*.jar", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var info = new FileInfo(path);
                    if (info.Length == 0 || info.Length > 200L * 1024 * 1024)
                        continue;
                    string sha1 = Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData(File.ReadAllBytes(path)));
                    files.Add(new ModFile(info.Name, sha1, info.Length));
                    paths?.TryAdd(sha1, path);
                }
                catch (IOException ex)
                {
                    LinkLog.Warn($"读取模组 {Path.GetFileName(path)} 失败:{ex.Message}");
                }
            }
        }
        files.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return new ModList(gameVersion, loader, files);
    }

    /// <summary>
    /// Joins a room: the network comes up, the room service is reached through a local forward, and the game
    /// port is forwarded the same way, so the game joins <c>127.0.0.1</c>.
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

            var config = CreateConfig(code, "salmon-guest-" + Hex.Random(4), null);
            session._network = EasyTierInstance.Start(config);
            session.HookNetwork();

            var deadline = DateTime.UtcNow.AddSeconds(Math.Max(30, LinkSettings.Current.SearchTimeoutSeconds));
            var host = await FindHostAsync(session._network, deadline, session.Log, token).ConfigureAwait(false);

            session._localServicePort = EasyTierInstance.FindFreePort();
            if (!await session._network.AddPortForwardAsync("tcp",
                    new IPEndPoint(IPAddress.Loopback, session._localServicePort), host, token).ConfigureAwait(false))
                throw new LinkException("无法建立房间转发,请检查网络或关闭占用端口的程序");

            session._client = await ConnectRoomAsync(session._localServicePort, deadline, session.Log, token).ConfigureAwait(false);
            session._room = await session._client.GetRoomInfoAsync(token).ConfigureAwait(false);
            session._gamePort = await session._client.GetServerPortAsync(token).ConfigureAwait(false);
            session._clientPlayers = await session._client.GetPlayersAsync(token).ConfigureAwait(false);

            // the game joins a local port; the network carries it to the host's game port
            session._localGamePort = EasyTierInstance.FindFreePort();
            var gameTarget = new IPEndPoint(host.Address, session._gamePort);
            bool forwarded =
                await session._network.AddPortForwardAsync("tcp", new IPEndPoint(IPAddress.Loopback, session._localGamePort), gameTarget, token).ConfigureAwait(false)
                && await session._network.AddPortForwardAsync("udp", new IPEndPoint(IPAddress.Loopback, session._localGamePort), gameTarget, token).ConfigureAwait(false);
            if (!forwarded)
                session.Log?.Invoke("游戏端口转发失败,可能需要以管理员身份运行才能转发 UDP");

            session.StartHeartbeat();
            Current = session;
            session.SetStatus(LinkStatus.Online);
            session.Log?.Invoke($"已加入房间「{session._room.Name}」· 游戏地址 {session.JoinAddress}");
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<RoomClient> ConnectRoomAsync(
        int localPort, DateTime deadline, Action<string>? log, CancellationToken token)
    {
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var client = await RoomClient.TryConnectAsync(localPort, token).ConfigureAwait(false);
                if (client is not null)
                    return client;
            }
            catch (RoomRejectedException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                last = ex;
                log?.Invoke("房主尚未响应,继续等待…");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
        }

        throw new LinkException("房间无响应,请确认房间码正确、双方网络正常", last);
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

    /// <summary>Host only: points the room at the world the host just opened.</summary>
    public void BindGamePort(int port, string worldName)
    {
        if (_server is null || port is < 1 or > 65535 || port == _gamePort)
            return;

        _gamePort = port;
        _server.UpdateMinecraftPort(port);
        SetStatus(LinkStatus.Hosting);
        Log?.Invoke($"已绑定世界「{worldName}」· 端口 {port}");
        RaiseChanged();
    }

    /// <summary>Takes a player out of the room and remembers it, so the same code cannot walk back in.</summary>
    public void BlockAndKick(string machineId, string playerName)
    {
        if (string.IsNullOrEmpty(machineId))
            return;
        Blocklist.Add(new PlayerProfile(playerName, machineId, "", PlayerKind.Guest));
        _server?.RemoveSilentGuests();
        Log?.Invoke($"已移出 {playerName},并禁止其再次加入");
    }

    private void StartHeartbeat()
    {
        _heartbeat = new CancellationTokenSource();
        var token = _heartbeat.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                    var client = _client;
                    if (client is null)
                        continue;
                    await client.SendHeartbeatAsync(_me.WithKind(PlayerKind.Local), token).ConfigureAwait(false);
                    _clientPlayers = await client.GetPlayersAsync(token).ConfigureAwait(false);
                    RaiseChanged();
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex) when (RoomClient.IsConnectionError(ex))
                {
                    if (!_stopped)
                    {
                        Log?.Invoke("与房主的连接已断开");
                        SetStatus(LinkStatus.Disconnected, "与房主的连接已断开");
                    }
                    return;
                }
                catch (Exception)
                {
                    // a single failed heartbeat is not fatal
                }
            }
        }, token);
    }

    private void OnPlayersChanged(object? sender, EventArgs e) => RaiseChanged();

    private void HookNetwork()
    {
        if (_network is null)
            return;
        _network.Exited += (_, _) =>
        {
            if (_stopped)
                return;
            SetStatus(LinkStatus.Disconnected, "联机内核已退出");
        };
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

    /// <summary>Asks the host which mods the room runs; <c>null</c> while pending or when sharing is off.</summary>
    public async Task<ModList?> FetchModsAsync(CancellationToken token = default)
    {
        var client = _client;
        if (client is null)
            return null;
        try
        {
            var (list, off) = await client.GetModListAsync(token).ConfigureAwait(false);
            return off ? null : list;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Copies the host's mods into <paramref name="targetDirectory"/>, verifying every file.</summary>
    public async Task<int> DownloadModsAsync(string targetDirectory, IProgress<double>? progress, CancellationToken token)
    {
        var client = _client ?? throw new LinkException("只有加入房间后才能下载房主的模组");
        var (list, off) = await client.GetModListAsync(token).ConfigureAwait(false);
        if (off || list is null || list.Files.Count == 0)
            return 0;

        Directory.CreateDirectory(targetDirectory);
        long total = list.Files.Sum(f => Math.Max(0, f.Size));
        long copied = 0;
        int written = 0;

        foreach (var file in list.Files)
        {
            token.ThrowIfCancellationRequested();
            string target = Path.Combine(targetDirectory, Path.GetFileName(file.Name));
            if (Matches(target, file))
            {
                copied += file.Size;
                progress?.Report(total == 0 ? 1 : (double)copied / total);
                continue;
            }

            string temporary = target + ".download";
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                long offset = 0;
                while (true)
                {
                    var chunk = await client.GetModChunkAsync(file.Sha1, offset, token).ConfigureAwait(false);
                    if (chunk.Length == 0)
                        break;
                    await stream.WriteAsync(chunk, token).ConfigureAwait(false);
                    offset += chunk.Length;
                    copied += chunk.Length;
                    progress?.Report(total == 0 ? 1 : (double)copied / total);
                }
            }

            if (!Matches(temporary, file))
            {
                try { File.Delete(temporary); } catch { }
                throw new LinkException($"模组 {file.Name} 校验失败,已丢弃");
            }

            File.Move(temporary, target, overwrite: true);
            written++;
        }

        Log?.Invoke($"已同步 {written} 个模组到 {targetDirectory}");
        return written;
    }

    private static bool Matches(string path, ModFile file)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != file.Size)
            return false;
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData(stream)) == file.Sha1;
        }
        catch (IOException)
        {
            return false;
        }
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
            throw new LinkException("联机组件无法在当前系统运行,请检查杀毒软件拦截");
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
            NoTun = true,   // traffic to the room travels through local port forwards, so no adapter is needed
            DirectOnly = !settings.UsePublicNodes,
            HideAddress = settings.HideAddress,
        };
        foreach (string peer in LinkSettings.GetNodes())
            config.Peers.Add(peer);
        return config;
    }

    /// <summary>Ends the session: drops the guests, closes the room service and stops the virtual network.</summary>
    public async Task StopAsync()
    {
        if (_stopped)
            return;
        _stopped = true;

        if (Current == this)
            Current = null;

        try { _heartbeat?.Cancel(); } catch { }

        if (_server is not null)
            _server.PlayersChanged -= OnPlayersChanged;

        _client?.Dispose();
        _client = null;

        if (_server is not null)
        {
            try { _server.Dispose(); } catch { }
            _server = null;
        }

        _network?.Dispose();
        _network = null;

        _heartbeat?.Dispose();
        _heartbeat = null;
        _status = LinkStatus.Idle;
        Error = null;
        Log?.Invoke("联机已停止");
        RaiseChanged();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    public string MyId => _me.MachineId;
}
