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
using Windows.Storage.Pickers;

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
        Id = player.Id;
        Name = player.Name;
        Tag = player.IsHost
            ? Loc.T("房主")
            : player.Account == AccountKind.Microsoft ? Loc.T("正版") : Loc.T("离线");
        Glyph = player.IsHost ? "\uE7FC" : "\uE77B";
        CanKick = kick is not null && !player.IsHost ? Visibility.Visible : Visibility.Collapsed;
        KickCommand = new RelayCommand(() => kick?.Invoke(player));
    }

    public string Id { get; }
    public string Name { get; }
    public string Tag { get; }
    public string Glyph { get; }
    public Visibility CanKick { get; }
    public RelayCommand KickCommand { get; }
}

/// <summary>One room as the lobby lists it.</summary>
public sealed class LobbyRoomRow
{
    public LobbyRoomRow(LobbyRoom room, Action<LobbyRoom> join)
    {
        Room = room;
        Name = room.Name;
        string tags = room.Tags.Count > 0 ? string.Join(" · ", room.Tags) + " · " : "";
        string version = room.Version.Length > 0 ? room.Version : "?";
        Detail = string.Format("{0}{1}/{2} · {3}{4}", tags, room.Players, room.MaxPlayers, version,
            room.HasPassword ? " · 🔒" : "");
        JoinCommand = new RelayCommand(() => join(room));
    }

    public LobbyRoom Room { get; }
    public string Name { get; }
    public string Detail { get; }
    public RelayCommand JoinCommand { get; }
}

/// <summary>
/// Multiplayer in the shape players expect from a room-code tool: open the room first and share the code,
/// then enter a world — the launcher notices the world by itself and points the room at it. A guest pastes
/// the code and is launched straight into the host's world.
/// </summary>
public sealed partial class MultiplayerPage : Page
{
    private readonly DispatcherQueue _ui = DispatcherQueue.GetForCurrentThread();
    private readonly ObservableCollection<string> _messages = [];
    private readonly ObservableCollection<LanWorldRow> _worlds = [];
    private readonly ObservableCollection<LinkPlayerRow> _players = [];
    private readonly ObservableCollection<LobbyRoomRow> _lobbyRooms = [];
    private readonly LanWorldFinder _finder = new();

    private LinkSession? _session;
    private CancellationTokenSource? _cts;
    private string _playerSignature = "";
    private int _lastPlayerCount = -1;
    private bool _busy;
    private bool _modsProbed;
    private bool _loadingSettings;
    private string _lobbyId = "";
    private string _lobbySecret = "";
    private DispatcherQueueTimer? _lobbyTimer;
    private DispatcherQueueTimer? _statusTimer;

    public MultiplayerPage()
    {
        InitializeComponent();
        LogList.ItemsSource = _messages;
        LanWorldList.ItemsSource = _worlds;
        HostPlayersList.ItemsSource = _players;
        JoinPlayersList.ItemsSource = _players;
        LobbyList.ItemsSource = _lobbyRooms;
        ModeCreate.IsChecked = true;

        _finder.Found += world => _ui.TryEnqueue(() => AddWorld(world));
        _finder.Failed += reason => _ui.TryEnqueue(() => AddLog("[局域网] " + reason));

        _lobbyTimer = DispatcherQueue.CreateTimer();
        _lobbyTimer.Interval = TimeSpan.FromSeconds(60);
        _lobbyTimer.IsRepeating = true;
        _lobbyTimer.Tick += async (_, _) => await LobbyTickAsync();

        // the companion mod reports the LAN port directly, which also covers networks where the multicast
        // announcement never arrives; without the mod this reads a file that does not exist
        _statusTimer = DispatcherQueue.CreateTimer();
        _statusTimer.Interval = TimeSpan.FromSeconds(5);
        _statusTimer.IsRepeating = true;
        _statusTimer.Tick += (_, _) => StatusTick();

        PageScroll.SizeChanged += (_, _) => FitToViewport();
        Loaded += (_, _) =>
        {
            FitToViewport();
            LoadSettingsIntoUi();
        };
    }

    /// <summary>Keeps the content exactly as wide as the scroll viewport so every line wraps.</summary>
    private void FitToViewport()
    {
        double width = ActualWidth - 72;
        if (width > 320)
            ContentRoot.Width = width;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        UpdateEnvironment();
        StartLanScan();
        Attach(LinkSession.Current);
        Refresh();
        _lobbyTimer?.Start();
        _statusTimer?.Start();
        AddLog(Loc.T("联机模块就绪"));
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        // the room keeps running while the player visits other pages; only the scanner and the lobby poll stop here
        StopLanScan();
        _lobbyTimer?.Stop();
        _statusTimer?.Stop();
    }

    // ------------------------------------------------------------ settings

    private void LoadSettingsIntoUi()
    {
        _loadingSettings = true;
        var settings = LinkSettings.Current;
        PlayerNameBox.Text = settings.PlayerName;
        CustomNodesBox.Text = settings.CustomNodes;
        PublicNodesSwitch.IsOn = settings.UsePublicNodes;
        ShareModsSwitch.IsOn = settings.ShareMods;
        HideAddressSwitch.IsOn = settings.HideAddress;
        UseLobbySwitch.IsOn = settings.UseLobby;
        LobbyTagsBox.Text = string.Join(", ", settings.LobbyTags);
        LobbyPasswordBox.Text = settings.LobbyPassword;
        LobbyUrlBox.Text = settings.LobbyUrl;
        CorePathBox.Text = settings.CorePath;
        MirrorBox.Text = settings.DownloadMirror;

        TimeoutCombo.Items.Clear();
        foreach (int seconds in LinkSettings.SearchTimeoutChoices)
            TimeoutCombo.Items.Add(string.Format(Loc.T("{0} 秒"), seconds));
        int index = Array.IndexOf(LinkSettings.SearchTimeoutChoices, settings.SearchTimeoutSeconds);
        TimeoutCombo.SelectedIndex = index >= 0 ? index : 1;

        InstanceCombo.Items.Clear();
        foreach (string instance in KnownInstances())
            InstanceCombo.Items.Add(instance);
        string preferred = LaunchPipeline.LatestInstanceId() ?? "";
        InstanceCombo.SelectedItem = InstanceCombo.Items.Contains(preferred)
            ? preferred
            : InstanceCombo.Items.FirstOrDefault();
        _loadingSettings = false;
    }

    /// <summary>Instance ids that actually have a game jar, newest first.</summary>
    private static IEnumerable<string> KnownInstances()
    {
        string root = Runtime.InstancesDir;
        if (!Directory.Exists(root))
            return [];
        return Directory.EnumerateDirectories(root)
            .Select(Path.GetFileName)
            .Where(id => id is not null && File.Exists(Path.Combine(root, id, "versions", id + ".jar")))
            .Select(id => id!)
            .OrderByDescending(id => File.GetLastWriteTimeUtc(Path.Combine(root, id, "versions", id + ".jar")))
            .ToList();
    }

    private string SelectedInstance => InstanceCombo.SelectedItem as string
                                       ?? LaunchPipeline.LatestInstanceId()
                                       ?? "";

    /// <summary>Lets a player hand the launcher a core they downloaded themselves.</summary>
    private async void OnPickCore(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.Window));
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.FileTypeFilter.Add(".exe");
            var file = await picker.PickSingleFileAsync();
            if (file is null)
                return;
            CorePathBox.Text = file.Path;
            SetDetail(string.Format(Loc.T("已指定本地内核:{0}"), Path.GetFileName(file.Path)));
        }
        catch (Exception ex)
        {
            SetDetail(Loc.T("无法选择文件:") + ex.Message);
        }
    }

    private void OnClearCore(object sender, RoutedEventArgs e)
    {
        CorePathBox.Text = "";
        SetDetail(Loc.T("将自动下载联机内核"));
    }

    private void OnSettingTextChanged(object sender, TextChangedEventArgs e) => ApplySettings();

    private void OnSettingToggled(object sender, RoutedEventArgs e) => ApplySettings();

    private void OnSettingSelected(object sender, SelectionChangedEventArgs e) => ApplySettings();

    /// <summary>Writes the settings panel back to disk; everything in it takes effect on the next room.</summary>
    private void ApplySettings()
    {
        if (_loadingSettings)
            return;

        var settings = LinkSettings.Current;
        settings.PlayerName = PlayerNameBox.Text.Trim();
        settings.CustomNodes = CustomNodesBox.Text.Trim();
        settings.UsePublicNodes = PublicNodesSwitch.IsOn;
        settings.ShareMods = ShareModsSwitch.IsOn;
        settings.ServeMods = ShareModsSwitch.IsOn;
        settings.HideAddress = HideAddressSwitch.IsOn;
        settings.UseLobby = UseLobbySwitch.IsOn;
        settings.LobbyTags = LobbyTagsBox.Text
            .Split([',', '，', ' ', '、'], StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Take(5)
            .ToList();
        settings.LobbyPassword = LobbyPasswordBox.Text.Trim();
        settings.LobbyUrl = LobbyUrlBox.Text.Trim();
        settings.CorePath = CorePathBox.Text.Trim();
        settings.DownloadMirror = MirrorBox.Text.Trim();
        if (TimeoutCombo.SelectedIndex >= 0 && TimeoutCombo.SelectedIndex < LinkSettings.SearchTimeoutChoices.Length)
            settings.SearchTimeoutSeconds = LinkSettings.SearchTimeoutChoices[TimeoutCombo.SelectedIndex];
        LinkSettings.Save();

        Refresh();
    }

    // ------------------------------------------------------------ environment

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
        if (create)
        {
            StartLanScan();   // the host side keeps watching for worlds
        }
        else if (_session is not { Role: LinkRole.Host })
        {
            // a host stays on the scanner even while looking at the join tab: its world may still change
            StopLanScan();
        }
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

        // the host should never have to type a port: the newest world becomes the room's target
        if (_session is { Role: LinkRole.Host, Status: not LinkStatus.Disconnected })
            _session.BindGamePort(world.Port, world.Motd);
    }

    private void OnLanWorldClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not LanWorldRow row)
            return;
        if (_session is { Role: LinkRole.Host })
            _session.BindGamePort(row.World.Port, row.Motd);
        else if (RoomNameBox.Text.Trim().Length == 0)
            RoomNameBox.Text = row.Motd;
    }

    private void OnRescanLan(object sender, RoutedEventArgs e)
    {
        _worlds.Clear();
        LanWorldList.Visibility = Visibility.Collapsed;
        LanEmptyText.Visibility = Visibility.Visible;
        StopLanScan();
        StartLanScan();
        SetDetail(Loc.T("正在重新扫描局域网…"));
    }

    // ------------------------------------------------------------ create room

    private async void OnHost(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;
        if (!LinkSession.CanRun(out string reason))
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("无法创建房间"), reason, MsgMark.Warning);
            return;
        }

        BeginBusy(host: true);
        try
        {
            var request = new HostRequest(
                RoomNameBox.Text.Trim(),
                NoticeBox.Text.Trim(),
                ReadMaxPlayers(),
                BuildModShare());

            var progress = new Progress<double>(value => ShowDownloadProgress(value));
            var session = await LinkSession.HostAsync(request, BuildProfile(), progress, Token).ConfigureAwait(true);
            Attach(session);
            SetDetail(null);
            Refresh();
            await InstallCompanionAsync().ConfigureAwait(true);
            await PublishLobbyAsync().ConfigureAwait(true);
            AppServices.Notify.Publish(NotifyKind.Success, Loc.T("房间已创建,把房间码发给好友即可"));
        }
        catch (OperationCanceledException)
        {
            AddLog(Loc.T("已取消创建房间"));
            SetDetail(Loc.T("已取消"));
        }
        catch (Exception ex)
        {
            AppLog.Warn("Multiplayer", "创建房间失败:" + ex.Message);
            AddLog("[错误] " + ex.Message);
            SetDetail(Loc.T("创建房间失败:") + ex.Message);
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("创建房间失败:") + ex.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    private void OnCancelHost(object sender, RoutedEventArgs e) => CancelCurrent();

    private async void OnStopHost(object sender, RoutedEventArgs e)
    {
        var session = _session;
        if (session is null)
            return;

        Attach(null);
        await UnpublishLobbyAsync().ConfigureAwait(true);
        await session.StopAsync().ConfigureAwait(true);
        CompanionBridge.ClearRoom();
        _worlds.Clear();
        Refresh();
        SetDetail(Loc.T("联机已停止"));
        AppServices.Notify.Publish(NotifyKind.Info, Loc.T("联机已停止"));
    }

    // ------------------------------------------------------------ join room

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

        await JoinAsync(code).ConfigureAwait(true);
    }

    private async Task JoinAsync(RoomCode code)
    {
        BeginBusy(host: false);
        try
        {
            var progress = new Progress<double>(value => ShowDownloadProgress(value));
            var session = await LinkSession.JoinAsync(code, BuildProfile(), progress, Token).ConfigureAwait(true);
            Attach(session);
            SetDetail(null);
            Refresh();
            AppServices.Notify.Publish(NotifyKind.Success, Loc.T("已加入房间"));
        }
        catch (OperationCanceledException)
        {
            AddLog(Loc.T("已取消加入"));
            SetDetail(Loc.T("已取消"));
        }
        catch (RoomRejectedException rejected)
        {
            string message = rejected.Full
                ? Loc.T("房间人数已满")
                : Loc.T("房主已把你移出房间,无法再次加入");
            AddLog("[错误] " + message);
            SetDetail(message);
            AppServices.Notify.Publish(NotifyKind.Error, message);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Multiplayer", "加入房间失败:" + ex.Message);
            AddLog("[错误] " + ex.Message);
            SetDetail(Loc.T("加入失败:") + ex.Message);
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("加入失败:") + ex.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    private void OnCancelJoin(object sender, RoutedEventArgs e) => CancelCurrent();

    /// <summary>Retries the room the window still remembers, without making the player paste the code again.</summary>
    private async void OnRejoin(object sender, RoutedEventArgs e)
    {
        var code = _session?.Code ?? RoomCode.Find(CodeBox.Text);
        if (code is null)
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("没有可用的房间码"),
                Loc.T("请重新粘贴房主发来的房间码。"), MsgMark.Warning);
            return;
        }

        if (_session is { } previous)
        {
            Attach(null);
            await previous.StopAsync().ConfigureAwait(true);
            Refresh();
        }

        CodeBox.Text = code.Code;
        await JoinAsync(code).ConfigureAwait(true);
    }

    private void OnPasteCode(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.GetContent().Contains(StandardDataFormats.Text))
            {
                _ = PasteAsync();
                return;
            }
        }
        catch (Exception)
        {
            // the clipboard is shared; a failure here just means "nothing to paste"
        }
        SetDetail(Loc.T("剪贴板里没有文字"));
    }

    private async Task PasteAsync()
    {
        try
        {
            string text = await Clipboard.GetContent().GetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
                CodeBox.Text = text.Trim();
        }
        catch (Exception)
        {
            SetDetail(Loc.T("剪贴板里没有文字"));
        }
    }

    // ------------------------------------------------------------ session plumbing

    private CancellationToken Token => (_cts ??= new CancellationTokenSource()).Token;

    private void BeginBusy(bool host)
    {
        _busy = true;
        _cts = new CancellationTokenSource();
        HostButton.IsEnabled = false;
        JoinButton.IsEnabled = false;
        CancelHostButton.Visibility = host ? Visibility.Visible : Visibility.Collapsed;
        CancelJoinButton.Visibility = host ? Visibility.Collapsed : Visibility.Visible;
        (host ? HostProgress : JoinProgress).Visibility = Visibility.Visible;
        SetDetail(host ? Loc.T("正在准备房间…") : Loc.T("正在连接房主…"));
    }

    private void EndBusy()
    {
        _busy = false;
        _cts?.Dispose();
        _cts = null;
        HostButton.IsEnabled = true;
        JoinButton.IsEnabled = true;
        CancelHostButton.Visibility = Visibility.Collapsed;
        CancelJoinButton.Visibility = Visibility.Collapsed;
        HostProgress.Visibility = Visibility.Collapsed;
        JoinProgress.Visibility = Visibility.Collapsed;
    }

    private void CancelCurrent()
    {
        try { _cts?.Cancel(); } catch { }
    }

    /// <summary>Shows the runtime download, which is the only long part before the room exists.</summary>
    private void ShowDownloadProgress(double value)
    {
        if (value <= 0.001 || value >= 0.999)
            return;
        SetDetail(string.Format(Loc.T("正在下载联机内核…{0}%"), (int)(value * 100)));
        if (_busy)
            (CancelJoinButton.Visibility == Visibility.Visible ? JoinProgress : HostProgress).Value = value * 100;
    }

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
        _playerSignature = "";
        _lastPlayerCount = -1;

        if (_session is null)
            return;

        _session.Changed += OnSessionChanged;
        _session.Log += OnSessionLog;
        if (_session.Code is { } code)
            CodeBox.Text = code.Code;
    }

    private void OnSessionChanged() => _ui.TryEnqueue(Refresh);

    private void OnSessionLog(string line) => _ui.TryEnqueue(() => AddLog(line));

    // ------------------------------------------------------------ rendering

    /// <summary>Redraws everything that depends on the room in progress.</summary>
    private void Refresh()
    {
        if (_loadingSettings)
            return;

        var session = _session;
        bool host = session?.Role == LinkRole.Host;
        bool guest = session?.Role == LinkRole.Guest;
        bool lost = session?.Status == LinkStatus.Disconnected;
        LinkStatus status = session?.Status ?? LinkStatus.Idle;

        HostForm.Visibility = host ? Visibility.Collapsed : Visibility.Visible;
        RoomCard.Visibility = host ? Visibility.Visible : Visibility.Collapsed;
        StopHostButton.Visibility = host ? Visibility.Visible : Visibility.Collapsed;
        HostButton.Visibility = host ? Visibility.Collapsed : Visibility.Visible;

        JoinCard.Visibility = guest && !lost ? Visibility.Visible : Visibility.Collapsed;
        DisconnectedCard.Visibility = guest && lost ? Visibility.Visible : Visibility.Collapsed;
        LeaveButton.Visibility = guest && !lost ? Visibility.Visible : Visibility.Collapsed;
        JoinButton.Visibility = guest ? Visibility.Collapsed : Visibility.Visible;
        DisconnectedText.Text = session?.Error ?? Loc.T("与房间的连接已断开");

        if (session is null)
        {
            StatusText.Text = Loc.T("创建一个房间,或用房间码加入好友的房间");
            ClearPlayers();
            return;
        }

        if (host)
        {
            bool hideAddress = LinkSettings.Current.HideAddress;
            HostCodeText.Text = session.Code?.Code ?? "";
            HostStateText.Text = session.WorldReady
                ? string.Format(Loc.T("已绑定世界 · 端口 {0}"), session.GamePort)
                : Loc.T("等待你在游戏里进入世界…房间码可以先发给好友");
            HostAddressPanel.Visibility = hideAddress || !session.WorldReady ? Visibility.Collapsed : Visibility.Visible;
            HostAddressText.Text = session.WorldReady ? session.JoinAddress : "";
            StatusText.Text = status switch
            {
                LinkStatus.Preparing => Loc.T("正在准备虚拟局域网…"),
                LinkStatus.WaitingForWorld => Loc.T("房间已开,等待你进入世界"),
                LinkStatus.Hosting => string.Format(Loc.T("房间进行中 · 游戏端口 {0} · 成员 {1} 人"), session.GamePort, session.Players.Count),
                LinkStatus.Disconnected => Loc.T("房间已断开:") + (session.Error ?? Loc.T("未知原因")),
                _ => Loc.T("房间已开"),
            };
        }
        else
        {
            JoinRoomNameText.Text = session.Room.Name.Length > 0 ? session.Room.Name : Loc.T("未命名房间");
            JoinNoticeText.Text = session.Room.Notice.Length > 0 ? session.Room.Notice : Loc.T("房主没有留言");
            JoinVersionText.Text = VersionHint(session);
            JoinAddressText.Text = LinkSettings.Current.HideAddress ? "" : Loc.T("地址:") + session.JoinAddress;
            StatusText.Text = status switch
            {
                LinkStatus.Preparing => Loc.T("正在连接房主…"),
                LinkStatus.Online => string.Format(Loc.T("已加入房间 · 游戏端口 {0} · 成员 {1} 人"), session.GamePort, session.Players.Count),
                LinkStatus.Disconnected => Loc.T("与房间的连接已断开"),
                _ => Loc.T("已加入房间"),
            };
            if (!_modsProbed)
                _ = RefreshModsButtonAsync(session);
        }

        UpdatePlayers(session);
    }

    /// <summary>Says plainly when the guest would enter with a different game or mod set than the host.</summary>
    private string VersionHint(LinkSession session)
    {
        string mine = SelectedInstance;
        string hostGame = session.Players.FirstOrDefault(p => p.IsHost)?.GameVersion ?? "";
        if (mine.Length == 0 || hostGame.Length == 0)
            return "";
        return string.Equals(mine, hostGame, StringComparison.OrdinalIgnoreCase)
            ? string.Format(Loc.T("版本一致:{0}"), mine)
            : string.Format(Loc.T("版本可能不一致:你是 {0},房主是 {1}"), mine, hostGame);
    }

    /// <summary>Rebuilds the member list only when it actually changed, so a heartbeat cannot make it flicker.</summary>
    private void UpdatePlayers(LinkSession session)
    {
        string signature = string.Join('|', session.Players
            .Select(p => $"{p.Id}:{p.Name}:{p.IsHost}:{p.Account}"));
        if (signature == _playerSignature)
            return;

        _playerSignature = signature;
        bool host = session.Role == LinkRole.Host;

        _players.Clear();
        foreach (var player in session.Players)
            _players.Add(new LinkPlayerRow(player, host ? Kick : null));

        int count = _players.Count;
        if (count != _lastPlayerCount)
        {
            _lastPlayerCount = count;
            AddLog(string.Format(Loc.T("[房间] 当前 {0} 人"), count));
        }

        if (session.Role == LinkRole.Host)
        {
            string roomName = session.Room.Name.Length > 0 ? session.Room.Name : Loc.T("未命名房间");
            CompanionBridge.WriteRoom(roomName, session.Players.FirstOrDefault(p => p.IsHost)?.Name ?? "",
                session.Players.Select(p => p.Name));
        }
    }

    private void ClearPlayers()
    {
        _playerSignature = "";
        _lastPlayerCount = -1;
        _players.Clear();
    }

    private void Kick(PlayerProfile player)
    {
        _session?.BlockAndKick(player.Id, player.Name);
        AddLog(string.Format(Loc.T("[房间] 已移出 {0}"), player.Name));
    }

    /// <summary>Asks the host for its mod list once per session and reveals the sync button when there is one.</summary>
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

    // ------------------------------------------------------------ copy / launch / mods

    private void OnCopyCode(object sender, RoutedEventArgs e)
    {
        if (_session?.Code is not { } code)
            return;
        Copy(code.Code);
        SetDetail(Loc.T("房间码已复制:") + code.Code);
    }

    /// <summary>Copies a ready-to-send invitation instead of a bare code.</summary>
    private void OnCopyInvite(object sender, RoutedEventArgs e)
    {
        if (_session?.Code is not { } code)
            return;
        Copy(string.Format(Loc.T("我在 SalmonLauncher 开了一个房间,房间码:{0} —— 打开「联机 → 加入房间」粘贴即可进来。"), code.Code));
        SetDetail(Loc.T("邀请已复制,直接发给好友即可"));
    }

    private async void OnLaunchGame(object sender, RoutedEventArgs e) => await LaunchGameAsync(_session?.JoinAddress);

    private async void OnLaunchHost(object sender, RoutedEventArgs e) => await LaunchGameAsync(null);

    /// <summary>Starts the picked instance, optionally walking straight into a room once the game is up.</summary>
    private async Task LaunchGameAsync(string? joinServer)
    {
        string instanceId = SelectedInstance;
        if (instanceId.Length == 0)
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
            await InstallCompanionAsync().ConfigureAwait(true);
            string jvm = (cfg.JvmExtraArgs + " " + CompanionBridge.JvmArguments()).Trim();
            SetDetail(string.Format(Loc.T("正在启动 {0} 并进入房间…"), instanceId));
            var result = await LaunchPipeline.StartAsync(instanceId, accountName, cfg.MemoryMb,
                CancellationToken.None, identity, jvm, cfg.GameWidth, cfg.GameHeight,
                joinServer: joinServer ?? "").ConfigureAwait(true);

            if (result.Success)
            {
                SetDetail(string.Format(Loc.T("游戏运行中(PID {0})"), result.ProcessId));
                AppServices.Notify.Publish(NotifyKind.Success, Loc.T("游戏已启动,正在进入房间"));
            }
            else
            {
                SetDetail(Loc.T("启动失败:") + (result.Error ?? Loc.T("未知原因")));
                AppServices.Notify.Publish(NotifyKind.Error, Loc.T("启动失败:") + (result.Error ?? Loc.T("未知原因")));
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Multiplayer", ex);
            SetDetail(Loc.T("启动失败:") + ex.Message);
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("启动失败:") + ex.Message);
        }
    }

    private async void OnSyncMods(object sender, RoutedEventArgs e)
    {
        var session = _session;
        if (session is null || session.Role != LinkRole.Guest)
            return;

        string instanceId = SelectedInstance;
        if (instanceId.Length == 0)
        {
            await AppServices.Dialogs.AlertAsync(Loc.T("尚未安装任何版本"),
                Loc.T("请先安装一个实例,模组会同步到该实例的 mods 目录。"), MsgMark.Info);
            return;
        }

        try
        {
            string target = Path.Combine(Runtime.InstancesDir, instanceId, "mods");
            ModsButton.IsEnabled = false;
            var progress = new Progress<double>(value =>
                SetDetail(string.Format(Loc.T("正在同步房主模组…{0}%"), (int)(value * 100))));
            int written = await session.DownloadModsAsync(target, progress, CancellationToken.None).ConfigureAwait(true);
            SetDetail(string.Format(Loc.T("已同步 {0} 个模组 · 如游戏正在运行请重启后再进入"), written));
            AppServices.Notify.Publish(NotifyKind.Success, string.Format(Loc.T("已同步 {0} 个模组"), written));
        }
        catch (Exception ex)
        {
            SetDetail(Loc.T("同步失败:") + ex.Message);
            AppServices.Notify.Publish(NotifyKind.Error, Loc.T("同步失败:") + ex.Message);
        }
        finally
        {
            ModsButton.IsEnabled = true;
        }
    }

    /// <summary>Puts the companion mod and Fabric API into the instance the room will run.</summary>
    private async Task InstallCompanionAsync()
    {
        string instanceId = SelectedInstance;
        if (instanceId.Length == 0)
            return;

        if (!CompanionBridge.Supports(instanceId))
        {
            // the mod declares minecraft ~1.21.1 + fabricloader; anything else would just be a dead file
            AddLog(string.Format(Loc.T("[模组] {0} 不是 {1} 的 Fabric 实例,跳过伴随模组"),
                instanceId, CompanionBridge.TargetMinecraft));
            return;
        }

        string installed = CompanionBridge.EnsureInstalled(instanceId);
        if (installed.Length > 0)
            AddLog(string.Format(Loc.T("[模组] 已为 {0} 安装 {1}"), instanceId, installed));

        try
        {
            string api = await CompanionBridge.EnsureFabricApiAsync(instanceId, CancellationToken.None).ConfigureAwait(true);
            if (api.Length > 0)
                AddLog(string.Format(Loc.T("[模组] 已就绪 {0}"), api));
        }
        catch (Exception ex)
        {
            AddLog(Loc.T("[模组] Fabric API 安装失败:") + ex.Message);
        }
    }

    /// <summary>Picks up the port the companion mod reported, so a room binds even without the multicast.</summary>
    private void StatusTick()
    {
        if (_session is not { Role: LinkRole.Host } session || _busy)
            return;

        var status = CompanionBridge.ReadStatus();
        if (!status.Ready || status.Port == session.GamePort)
            return;

        session.BindGamePort(status.Port, status.Motd.Length > 0 ? status.Motd : "Minecraft");
    }

    // ------------------------------------------------------------ lobby

    /// <summary>Publishes or refreshes this machine's room in the lobby, when the player asked for that.</summary>
    private async Task PublishLobbyAsync()
    {
        var session = _session;
        var settings = LinkSettings.Current;
        if (session is not { Role: LinkRole.Host } || !settings.UseLobby || session.Code is not { } code)
            return;

        try
        {
            if (_lobbySecret.Length == 0)
                _lobbySecret = Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));

            var offer = new LobbyOffer(
                code.Code,
                session.Room.Name.Length > 0 ? session.Room.Name : Loc.T("未命名房间"),
                session.Room.Notice,
                settings.LobbyTags,
                session.Players.Count,
                session.Room.MaxPlayers,
                SelectedInstance,
                settings.LobbyPassword,
                _lobbySecret,
                600);

            _lobbyId = await LobbyClient.PublishAsync(offer, CancellationToken.None).ConfigureAwait(true);
            LobbyStatusText.Text = Loc.T("房间已发布到大厅");
        }
        catch (Exception ex)
        {
            LobbyStatusText.Text = Loc.T("发布到大厅失败:") + ex.Message;
        }
    }

    private async Task UnpublishLobbyAsync()
    {
        if (_lobbyId.Length == 0 || _lobbySecret.Length == 0)
            return;

        string id = _lobbyId;
        _lobbyId = "";
        try
        {
            await LobbyClient.UnpublishAsync(id, _lobbySecret, CancellationToken.None).ConfigureAwait(true);
            LobbyStatusText.Text = Loc.T("已从大厅移除");
        }
        catch (Exception ex)
        {
            AppLog.Warn("Multiplayer", "取消发布失败:" + ex.Message);
        }
    }

    /// <summary>Keeps the listing alive while hosting and refreshes the browsable list when it is open.</summary>
    private async Task LobbyTickAsync()
    {
        if (_session is { Role: LinkRole.Host } && LinkSettings.Current.UseLobby)
            await PublishLobbyAsync().ConfigureAwait(true);

        if (LobbyExpander.IsExpanded)
            await RefreshLobbyAsync().ConfigureAwait(true);
    }

    private async void OnRefreshLobby(object sender, RoutedEventArgs e) => await RefreshLobbyAsync().ConfigureAwait(true);

    private async Task RefreshLobbyAsync()
    {
        LobbyStatusText.Text = Loc.T("正在读取大厅…");
        try
        {
            var rooms = await LobbyClient.ListAsync(CancellationToken.None).ConfigureAwait(true);
            _lobbyRooms.Clear();
            foreach (var room in rooms)
                _lobbyRooms.Add(new LobbyRoomRow(room, room2 => JoinFromLobbyAsync(room2)));

            LobbyStatusText.Text = rooms.Count == 0
                ? Loc.T("大厅里暂时没有房间")
                : string.Format(Loc.T("大厅共 {0} 个房间"), rooms.Count);
        }
        catch (Exception ex)
        {
            LobbyStatusText.Text = Loc.T("读取大厅失败:") + ex.Message;
        }
    }

    /// <summary>Joins a listed room, asking for its password first when it has one.</summary>
    private async void JoinFromLobbyAsync(LobbyRoom room)
    {
        if (_busy)
            return;

        try
        {
            string codeText = room.Code;
            if (room.HasPassword)
            {
                string? password = await AppServices.Dialogs
                    .PromptAsync(Loc.T("房间密码"), Loc.T("这个房间设置了密码,请向房主索取"), "")
                    .ConfigureAwait(true);
                if (password is null)
                    return;
                codeText = await LobbyClient.RevealAsync(room.Id, password, CancellationToken.None).ConfigureAwait(true) ?? "";
                if (codeText.Length == 0)
                {
                    LobbyStatusText.Text = Loc.T("密码不正确");
                    return;
                }
            }

            var code = RoomCode.Find(codeText);
            if (code is null)
            {
                LobbyStatusText.Text = Loc.T("大厅返回的房间码无效");
                return;
            }

            CodeBox.Text = code.Code;
            await JoinAsync(code).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LobbyStatusText.Text = Loc.T("加入失败:") + ex.Message;
        }
    }

    // ------------------------------------------------------------ helpers

    private ModShare? BuildModShare()
    {
        var settings = LinkSettings.Current;
        string instanceId = SelectedInstance;
        if (!settings.ShareMods || !settings.ServeMods || instanceId.Length == 0)
            return null;

        // fingerprinting jars is disk work; keep it off the UI thread
        var share = ModShare.FromDirectory(Path.Combine(Runtime.InstancesDir, instanceId, "mods"), instanceId, "");
        return share.HasMods ? share : null;
    }

    private PlayerProfile BuildProfile()
    {
        var repo = AccountStore.Load();
        var active = repo.Accounts.FirstOrDefault(a => a.Id == repo.ActiveId);
        string accountName = active?.Name ?? AppServices.Settings.Current.AccountName;
        var profile = new PlayerProfile(LinkSettings.MachineId, LinkSettings.EffectivePlayerName(accountName));
        profile = profile.WithAccount(active?.Kind == "msa" ? AccountKind.Microsoft : AccountKind.Offline);
        if (SelectedInstance.Length > 0)
            profile = profile.WithGame(SelectedInstance);
        return profile;
    }

    private int ReadMaxPlayers()
    {
        double value = double.IsNaN(MaxPlayersBox.Value) ? 0 : MaxPlayersBox.Value;
        return (int)Math.Clamp(value, 0, 100);
    }

    private static void Copy(string text)
    {
        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        package.SetText(text);
        Clipboard.SetContent(package);
        Clipboard.Flush();
    }

    /// <summary>One-line status under the title for anything that is not the room state itself.</summary>
    private void SetDetail(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            DetailText.Text = "";
            DetailText.Visibility = Visibility.Collapsed;
            return;
        }
        DetailText.Text = text;
        DetailText.Visibility = Visibility.Visible;
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
