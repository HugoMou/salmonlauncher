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
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SL.App.Dialogs;
using SL.App.Infrastructure;
using SL.App.Localization;
using SL.App.Notify;
using SL.Core.Account;
using SL.Core.Boot;
using SL.Core.Launch;
using SL.Core.Link;
using SL.Core.Link.Room;
using SL.Core.Logging;
using Windows.ApplicationModel.DataTransfer;

namespace SL.App.Pages;

/// <summary>One world this machine currently has open to LAN.</summary>
public sealed class LanWorldRow
{
    public LanWorldRow(LanWorld world)
    {
        World = world;
        Motd = world.Motd;
        Address = $"{world.Address}:{world.Port}";
    }

    public LanWorld World { get; }
    public string Motd { get; }
    public string Address { get; }

    public override string ToString() => $"{Motd} · {Address}";
}

/// <summary>One member of a room, drawn the same way for host and guest.</summary>
public sealed class LinkPlayerRow
{
    public LinkPlayerRow(PlayerProfile player, Action<PlayerProfile>? kick)
    {
        Name = player.Name;
        Tag = player.IsHost
            ? Loc.T("房主")
            : player.Account == AccountKind.Microsoft ? Loc.T("正版") : Loc.T("离线");
        Glyph = player.IsHost ? "\uE7FC" : "\uE77B";
        CanKick = kick is not null && !player.IsHost ? Visibility.Visible : Visibility.Collapsed;
        KickCommand = new RelayCommand(() => kick?.Invoke(player));
    }

    public string Name { get; }
    public string Tag { get; }
    public string Glyph { get; }
    public Visibility CanKick { get; }
    public RelayCommand KickCommand { get; }
}

/// <summary>
/// Multiplayer: one player opens a room, everybody else joins it with a room code. The room runs on a
/// temporary virtual network built by EasyTier, so it works across the internet without port forwarding
/// while the host stays the only game server.
/// </summary>
public sealed partial class MultiplayerPage : Page
{
    private readonly DispatcherQueue _ui = DispatcherQueue.GetForCurrentThread();
    private readonly ObservableCollection<string> _messages = [];
    private readonly ObservableCollection<LanWorldRow> _worlds = [];
    private readonly ObservableCollection<LinkPlayerRow> _players = [];
    private readonly LanWorldFinder _finder = new();

    private LinkSession? _session;
    private bool _busy;
    private bool _modsProbed;

    public MultiplayerPage()
    {
        InitializeComponent();
        LogList.ItemsSource = _messages;
        LanWorldList.ItemsSource = _worlds;
        HostPlayersList.ItemsSource = _players;
        JoinPlayersList.ItemsSource = _players;
        ModeCreate.IsChecked = true;

        _finder.Found += world => _ui.TryEnqueue(() => AddWorld(world));
        _finder.Failed += reason => _ui.TryEnqueue(() => AddLog("[局域网] " + reason));

        // the page has to follow the window: text wraps instead of running off the right edge
        SizeChanged += (_, _) => FitToViewport();
        Loaded += (_, _) => FitToViewport();
    }

    /// <summary>Keeps the content exactly as wide as the scroll viewport so every line wraps.</summary>
    private void FitToViewport()
    {
        // the page itself is exactly the frame content area, which is the space this page may use
        double width = ActualWidth - 72;
        if (width > 320)
            ContentRoot.Width = width;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        GamePortBox.Header = Loc.T("游戏端口");
        RoomNameBox.Header = Loc.T("房间名");
        NoticeBox.Header = Loc.T("公告(可选)");
        MaxPlayersBox.Header = Loc.T("人数上限");
        CodeBox.Header = Loc.T("房间码");

        UpdateEnvironment();
        StartLanScan();
        Attach(LinkSession.Current);
        Refresh();
        AddLog(Loc.T("联机模块就绪"));
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        // the room keeps running while the player visits other pages; only the scanner is tied to this page
        StopLanScan();
    }

    /// <summary>Shows the administrator requirement once, and offers the restart that satisfies it.</summary>
    private void UpdateEnvironment()
    {
        bool ok = LinkSession.CanRun(out string reason);
        AdminNotice.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
        AdminNoticeText.Text = reason;
        ElevateButton.Visibility = ok || !OperatingSystem.IsWindows() ? Visibility.Collapsed : Visibility.Visible;
        HostButton.IsEnabled = ok;
        JoinButton.IsEnabled = ok;
    }

    private async void OnElevate(object sender, RoutedEventArgs e)
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
                return;
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" });
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("无法以管理员身份重启"), ex.Message, MsgMark.Warning);
        }
    }

    private void OnModeChecked(object sender, RoutedEventArgs e)
    {
        if (CreatePanel is null)
            return;
        bool create = ReferenceEquals(sender, ModeCreate);
        CreatePanel.Visibility = create ? Visibility.Visible : Visibility.Collapsed;
        JoinPanel.Visibility = create ? Visibility.Collapsed : Visibility.Visible;
        if (!create)
            StopLanScan();
    }

    // ------------------------------------------------------------ LAN worlds

    private void StartLanScan()
    {
        if (_finder.Running)
            return;
        _finder.Start();
        if (_finder.Running)
            AddLog(Loc.T("正在监听 224.0.2.60:4445 的开放世界公告…"));
    }

    private void StopLanScan()
    {
        if (_finder.Running)
            _finder.Dispose();
    }

    private void AddWorld(LanWorld world)
    {
        if (_worlds.Any(w => w.World.Key == world.Key))
            return;
        _worlds.Add(new LanWorldRow(world));
        LanWorldList.Visibility = Visibility.Visible;
        LanEmptyText.Visibility = Visibility.Collapsed;
        AddLog(string.Format(Loc.T("[局域网] 发现 {0} · {1}:{2}"), world.Motd, world.Address, world.Port));
    }

    private void OnLanWorldClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not LanWorldRow row)
            return;
        GamePortBox.Value = row.World.Port;
        if (RoomNameBox.Text.Trim().Length == 0)
            RoomNameBox.Text = row.Motd;
        AddLog(string.Format(Loc.T("[局域网] 已选择 {0} · 端口 {1}"), row.Motd, row.World.Port));
    }

    private void OnRescanLan(object sender, RoutedEventArgs e)
    {
        _worlds.Clear();
        LanWorldList.Visibility = Visibility.Collapsed;
        LanEmptyText.Visibility = Visibility.Visible;
        StopLanScan();
        StartLanScan();
    }

    // ------------------------------------------------------------ Create room

    private async void OnHost(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;
        if (!LinkSession.CanRun(out string reason))
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("无法创建房间"), reason, MsgMark.Warning);
            return;
        }
        if (!TryReadPort(GamePortBox, out int gamePort))
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("端口无效"),
                Loc.T("游戏端口需要在 1 到 65535 之间。请先在游戏里「对局域网开放」。"), MsgMark.Warning);
            return;
        }

        _busy = true;
        HostButton.IsEnabled = false;
        HostProgress.Visibility = Visibility.Visible;
        HostProgress.IsIndeterminate = false;
        HostProgress.Value = 0;
        StatusText.Text = Loc.T("正在准备虚拟局域网…");

        try
        {
            var me = BuildProfile();
            string instanceId = LaunchPipeline.LatestInstanceId() ?? "";
            var progress = new Progress<double>(value => HostProgress.Value = value * 100);
            var request = new HostRequest(
                gamePort,
                RoomNameBox.Text.Trim(),
                NoticeBox.Text.Trim(),
                ReadMaxPlayers(),
                BuildModShare(instanceId));

            var session = await LinkSession.HostAsync(request, me, progress, CancellationToken.None);
            Attach(session);
            Refresh();
            AppServices.Notify.Publish(NotifyKind.Success, Loc.T("房间已创建,把房间码发给好友即可"));
        }
        catch (Exception ex)
        {
            AppLog.Warn("Multiplayer", "创建房间失败:" + ex.Message);
            StatusText.Text = Loc.T("创建房间失败:") + ex.Message;
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("创建房间失败:") + ex.Message);
            AddLog("[错误] " + ex.Message);
        }
        finally
        {
            _busy = false;
            HostButton.IsEnabled = true;
            HostProgress.Visibility = Visibility.Collapsed;
        }
    }

    private ModShare? BuildModShare(string instanceId)
    {
        var settings = LinkSettings.Current;
        if (!settings.ShareMods || !settings.ServeMods || instanceId.Length == 0)
            return null;

        string modsDir = Path.Combine(Runtime.InstancesDir, instanceId, "mods");
        var share = ModShare.FromDirectory(modsDir, instanceId, "");
        return share.HasMods ? share : null;
    }

    // ------------------------------------------------------------ Join room

    private async void OnJoin(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;
        if (!LinkSession.CanRun(out string reason))
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("无法加入房间"), reason, MsgMark.Warning);
            return;
        }

        var code = RoomCode.Find(CodeBox.Text);
        if (code is null)
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("房间码无效"),
                Loc.T("房间码形如 U/XXXX-XXXX-XXXX-XXXX,请向房主确认后重新粘贴。"), MsgMark.Warning);
            return;
        }

        _busy = true;
        JoinButton.IsEnabled = false;
        JoinProgress.Visibility = Visibility.Visible;
        JoinProgress.IsIndeterminate = false;
        JoinProgress.Value = 0;
        StatusText.Text = Loc.T("正在加入虚拟局域网…");

        try
        {
            var progress = new Progress<double>(value => JoinProgress.Value = value * 100);
            var session = await LinkSession.JoinAsync(code, BuildProfile(), progress, CancellationToken.None);
            Attach(session);
            Refresh();
            AppServices.Notify.Publish(NotifyKind.Success, Loc.T("已加入房间"));
        }
        catch (RoomRejectedException rejected)
        {
            string message = rejected.Full ? Loc.T("房间人数已满") : Loc.T("房主已屏蔽你或把你移出了房间");
            StatusText.Text = message;
            AppServices.Notify.Publish(NotifyKind.Error, message);
            AddLog("[错误] " + message);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Multiplayer", "加入房间失败:" + ex.Message);
            StatusText.Text = Loc.T("加入失败:") + ex.Message;
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("加入失败:") + ex.Message);
            AddLog("[错误] " + ex.Message);
        }
        finally
        {
            _busy = false;
            JoinButton.IsEnabled = true;
            JoinProgress.Visibility = Visibility.Collapsed;
        }
    }

    // ------------------------------------------------------------ Session plumbing

    private void Attach(LinkSession? session)
    {
        if (ReferenceEquals(_session, session))
            return;

        if (_session is not null)
        {
            _session.Changed -= OnSessionChanged;
            _session.Log -= OnSessionLog;
        }

        _session = session;
        _modsProbed = false;

        if (_session is not null)
        {
            _session.Changed += OnSessionChanged;
            _session.Log += OnSessionLog;
            if (_session.Code is { } code)
                CodeBox.Text = code.Code;
        }
    }

    private void OnSessionChanged() => _ui.TryEnqueue(Refresh);

    private void OnSessionLog(string line) => _ui.TryEnqueue(() => AddLog(line));

    /// <summary>Redraws everything that depends on the room in progress.</summary>
    private void Refresh()
    {
        var session = _session;
        bool host = session?.Role == LinkRole.Host;
        bool guest = session?.Role == LinkRole.Guest;

        RoomCard.Visibility = host && session?.Code is not null ? Visibility.Visible : Visibility.Collapsed;
        StopHostButton.Visibility = host ? Visibility.Visible : Visibility.Collapsed;
        HostButton.Visibility = host ? Visibility.Collapsed : Visibility.Visible;
        JoinCard.Visibility = guest ? Visibility.Visible : Visibility.Collapsed;
        LeaveButton.Visibility = guest ? Visibility.Visible : Visibility.Collapsed;
        JoinButton.Visibility = guest ? Visibility.Collapsed : Visibility.Visible;

        if (session is null)
        {
            StatusText.Text = Loc.T("创建一个房间,或用房间码加入好友的房间");
            _players.Clear();
            return;
        }

        if (host)
        {
            HostCodeText.Text = session.Code?.Code ?? "";
            HostAddressText.Text = session.JoinAddress;
            StatusText.Text = string.Format(Loc.T("房间进行中 · 游戏端口 {0} · 成员 {1} 人"), session.GamePort, session.Players.Count);
        }
        else
        {
            JoinRoomNameText.Text = session.Room.Name.Length > 0
                ? session.Room.Name
                : Loc.T("未命名房间");
            JoinNoticeText.Text = session.Room.Notice.Length > 0 ? session.Room.Notice : Loc.T("房主没有留言");
            JoinAddressText.Text = session.JoinAddress;
            StatusText.Text = string.Format(Loc.T("已加入房间 · 游戏端口 {0} · 成员 {1} 人"), session.GamePort, session.Players.Count);
            // asking once per session is enough: the mod list cannot change while the room lasts
            if (!_modsProbed)
                _ = RefreshModsButtonAsync(session);
        }

        string? myId = session.MyId;
        _players.Clear();
        foreach (var player in session.Players)
            _players.Add(new LinkPlayerRow(player, host ? Kick : null));

        if (_players.Count == 0)
            AddLog(Loc.T("房间成员暂时没有更新"));

        // keep the host's own row first even when the server has not reported it yet
        if (host && _players.Count == 1 && myId is not null)
            StatusText.Text += Loc.T(" · 等待好友加入");
    }

    /// <summary>Shows the mod sync button only when the host actually offers mods.</summary>
    private async Task RefreshModsButtonAsync(LinkSession session)
    {
        _modsProbed = true;
        try
        {
            var mods = await session.FetchModsAsync().ConfigureAwait(true);
            ModsButton.Visibility = mods.Mods.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (mods.Mods.Count > 0)
                ModsButton.Content = string.Format(Loc.T("同步房主模组({0})"), mods.Mods.Count);
        }
        catch (Exception)
        {
            ModsButton.Visibility = Visibility.Collapsed;
        }
    }

    private void Kick(PlayerProfile player)
    {
        _session?.Kick(player.Id);
        AddLog(string.Format(Loc.T("[房间] 已移出 {0}"), player.Name));
    }

    private async void OnStopHost(object sender, RoutedEventArgs e)
    {
        var session = _session;
        if (session is null)
            return;

        Attach(null);
        await session.StopAsync();
        _players.Clear();
        Refresh();
        StatusText.Text = Loc.T("联机已停止");
        AppServices.Notify.Publish(NotifyKind.Info, Loc.T("联机已停止"));
    }

    // ------------------------------------------------------------ Copy / launch / mods

    private void OnCopyCode(object sender, RoutedEventArgs e)
    {
        if (_session?.Code is not { } code)
            return;
        Copy(code.Code);
        AppServices.Notify.Publish(NotifyKind.Success, Loc.T("房间码已复制:") + code.Code);
    }

    private void OnCopyAddress(object sender, RoutedEventArgs e)
    {
        if (_session is not { } session)
            return;
        Copy(session.JoinAddress);
        AppServices.Notify.Publish(NotifyKind.Success, Loc.T("地址已复制:") + session.JoinAddress);
    }

    private async void OnLaunchGame(object sender, RoutedEventArgs e)
    {
        var session = _session;
        if (session is null || session.Role != LinkRole.Guest)
            return;

        string? instanceId = LaunchPipeline.LatestInstanceId();
        if (string.IsNullOrEmpty(instanceId))
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("尚未安装任何版本"),
                Loc.T("请先到「下载 → 游戏版本」安装一个实例,再回来进入房间。"), MsgMark.Info);
            return;
        }

        try
        {
            var repo = AccountStore.Load();
            var active = repo.Accounts.FirstOrDefault(a => a.Id == repo.ActiveId);
            string accountName = active?.Name ?? AppServices.Settings.Current.AccountName;
            LaunchPipeline.LaunchIdentity? identity = null;

            if (active?.Kind == "msa")
            {
                if (active.Session is not { } msSession)
                {
                    AppServices.Notify.Publish(NotifyKind.Warning,
                        string.Format(Loc.T("账户 {0} 缺少完整会话,请到「外观与设置」重新登录。"), active.Name));
                    return;
                }
                msSession = await MsAuth.EnsureFreshAsync(msSession, null).ConfigureAwait(true);
                AccountStore.UpsertMsa(msSession);
                identity = new LaunchPipeline.LaunchIdentity(msSession.Name, msSession.Uuid, msSession.MinecraftAccessToken, "msa");
                accountName = msSession.Name;
            }

            var cfg = AppServices.Settings.Current;
            StatusText.Text = string.Format(Loc.T("正在启动 {0} 并进入房间…"), instanceId);
            var result = await LaunchPipeline.StartAsync(instanceId, accountName, cfg.MemoryMb,
                CancellationToken.None, identity, cfg.JvmExtraArgs, cfg.GameWidth, cfg.GameHeight,
                joinServer: session.JoinAddress).ConfigureAwait(true);

            if (result.Success)
            {
                AppServices.Notify.Publish(NotifyKind.Success, Loc.T("游戏已启动,正在进入房间"));
                StatusText.Text = string.Format(Loc.T("游戏运行中(PID {0})"), result.ProcessId);
            }
            else
            {
                StatusText.Text = Loc.T("启动失败:") + (result.Error ?? Loc.T("未知原因"));
                AppServices.Notify.Publish(NotifyKind.Error, StatusText.Text);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Multiplayer", ex);
            StatusText.Text = Loc.T("启动失败:") + ex.Message;
            AppServices.Notify.Publish(NotifyKind.Error, StatusText.Text);
        }
    }

    private async void OnSyncMods(object sender, RoutedEventArgs e)
    {
        var session = _session;
        if (session is null || session.Role != LinkRole.Guest)
            return;

        string? instanceId = LaunchPipeline.LatestInstanceId();
        if (string.IsNullOrEmpty(instanceId))
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("尚未安装任何版本"),
                Loc.T("请先安装一个实例,模组会同步到该实例的 mods 目录。"), MsgMark.Info);
            return;
        }

        try
        {
            string target = Path.Combine(Runtime.InstancesDir, instanceId, "mods");
            ModsButton.IsEnabled = false;
            var progress = new Progress<double>(value => StatusText.Text = string.Format(Loc.T("正在同步房主模组…{0}%"), (int)(value * 100)));
            int written = await session.DownloadModsAsync(target, progress, CancellationToken.None);
            StatusText.Text = string.Format(Loc.T("已同步 {0} 个模组"), written);
            AppServices.Notify.Publish(NotifyKind.Success, StatusText.Text);
        }
        catch (Exception ex)
        {
            StatusText.Text = Loc.T("同步失败:") + ex.Message;
            AppServices.Notify.Publish(NotifyKind.Error, StatusText.Text);
        }
        finally
        {
            ModsButton.IsEnabled = true;
        }
    }

    // ------------------------------------------------------------ Helpers

    private PlayerProfile BuildProfile()
    {
        var repo = AccountStore.Load();
        var active = repo.Accounts.FirstOrDefault(a => a.Id == repo.ActiveId);
        string accountName = active?.Name ?? AppServices.Settings.Current.AccountName;
        string name = LinkSettings.EffectivePlayerName(accountName);
        var profile = new PlayerProfile(LinkSettings.MachineId, name);
        return profile.WithAccount(active?.Kind == "msa" ? AccountKind.Microsoft : AccountKind.Offline);
    }

    private int ReadMaxPlayers()
    {
        double value = double.IsNaN(MaxPlayersBox.Value) ? 0 : MaxPlayersBox.Value;
        return (int)Math.Clamp(value, 0, 100);
    }

    private static bool TryReadPort(NumberBox box, out int port)
    {
        port = 0;
        if (double.TryParse(box.Text, out double typed))
            port = (int)Math.Clamp(typed, 1, 65535);
        else if (!double.IsNaN(box.Value))
            port = (int)Math.Clamp(box.Value, 1, 65535);
        return port is >= 1 and <= 65535;
    }

    private static void Copy(string text)
    {
        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        package.SetText(text);
        Clipboard.SetContent(package);
        Clipboard.Flush();
    }

    private void AddLog(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        _messages.Add($"{DateTime.Now:HH:mm:ss}  {text}");
        if (_messages.Count > 400)
            _messages.RemoveAt(0);
        LogScroll.ChangeView(null, LogScroll.ScrollableHeight, null);
    }
}

