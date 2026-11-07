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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SL.Core.Link;

/// <summary>Everything the link feature remembers between runs. Changes take effect on the next room.</summary>
public sealed class LinkSettingsData
{
    [JsonPropertyName("player")] public string PlayerName { get; set; } = "";
    [JsonPropertyName("lanEntryName")] public string LanEntryName { get; set; } = "";
    [JsonPropertyName("searchTimeout")] public int SearchTimeoutSeconds { get; set; } = 60;
    [JsonPropertyName("localPort")] public int LocalPort { get; set; }
    [JsonPropertyName("autoCopyCode")] public bool AutoCopyCode { get; set; } = true;
    [JsonPropertyName("autoOpenLan")] public bool AutoOpenLan { get; set; } = true;
    [JsonPropertyName("shareMods")] public bool ShareMods { get; set; } = true;
    [JsonPropertyName("serveMods")] public bool ServeMods { get; set; } = true;
    [JsonPropertyName("voice")] public bool Voice { get; set; }
    [JsonPropertyName("allowRelay")] public bool AllowRelay { get; set; } = true;
    [JsonPropertyName("usePublicNodes")] public bool UsePublicNodes { get; set; } = true;
    [JsonPropertyName("customNodes")] public string CustomNodes { get; set; } = "";
    [JsonPropertyName("disabledNodes")] public List<string> DisabledNodes { get; set; } = [];
    [JsonPropertyName("shareWithLan")] public bool ShareWithLan { get; set; } = true;
    [JsonPropertyName("hideAddress")] public bool HideAddress { get; set; }
    [JsonPropertyName("readGameAccount")] public bool ReadGameAccount { get; set; } = true;
    [JsonPropertyName("showSkins")] public bool ShowSkins { get; set; } = true;
    [JsonPropertyName("shareVersion")] public bool ShareVersion { get; set; } = true;
    [JsonPropertyName("maxPlayers")] public int MaxPlayers { get; set; }
    [JsonPropertyName("roomNotice")] public string RoomNotice { get; set; } = "";
    [JsonPropertyName("machineId")] public string MachineId { get; set; } = "";
    [JsonPropertyName("lobbyTitle")] public string LobbyTitle { get; set; } = "";
    [JsonPropertyName("lobbyDescription")] public string LobbyDescription { get; set; } = "";
    [JsonPropertyName("lobbyTags")] public List<string> LobbyTags { get; set; } = [];
}

/// <summary>
/// Multiplayer preferences and the identity of this computer. Stored as one JSON file in the link directory.
/// </summary>
public static class LinkSettings
{
    /// <summary>Choices offered for <see cref="LinkSettingsData.SearchTimeoutSeconds"/>.</summary>
    public static readonly int[] SearchTimeoutChoices = [30, 60, 120];

    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static LinkSettingsData? _data;

    /// <summary>Directory that holds link state (settings, downloaded runtime, logs).</summary>
    public static string DataDirectory => Path.Combine(Boot.Runtime.Root, "link");

    /// <summary>Folder the EasyTier runtime is unpacked into.</summary>
    public static string RuntimeDirectory => Path.Combine(DataDirectory, "runtime");

    private static string FilePath => Path.Combine(DataDirectory, "settings.json");

    /// <summary>The mutable settings object. Call <see cref="Save"/> after changing it.</summary>
    public static LinkSettingsData Current
    {
        get
        {
            lock (Gate)
                return _data ??= Load();
        }
    }

    public static void Save()
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(_data ?? new LinkSettingsData(), JsonOptions));
            }
            catch (Exception ex)
            {
                LinkLog.Warn("联机设置保存失败:" + ex.Message);
            }
        }
    }

    /// <summary>Identifier of this computer, created once and kept. Used as the player id in rooms.</summary>
    public static string MachineId
    {
        get
        {
            var data = Current;
            if (!LinkUtil.IsHex(data.MachineId, 32))
            {
                data.MachineId = LinkUtil.RandomHex(16);
                Save();
            }
            return data.MachineId.ToLowerInvariant();
        }
    }

    /// <summary>Player name to send; falls back to the launcher account name, then a machine-based default.</summary>
    public static string EffectivePlayerName(string? accountName)
    {
        string typed = Current.PlayerName.Trim();
        if (typed.Length > 0)
            return typed;
        if (!string.IsNullOrWhiteSpace(accountName))
            return accountName;
        return "Salmon #" + MachineId[..4].ToUpperInvariant();
    }

    /// <summary>Custom nodes first, then the enabled built-in nodes, without duplicates.</summary>
    public static IReadOnlyList<string> GetNodes()
    {
        var data = Current;
        var nodes = ParseNodes(data.CustomNodes, out _);
        if (data.UsePublicNodes)
        {
            foreach (var node in PublicNodes.All)
            {
                if (!data.DisabledNodes.Contains(node.Id))
                    nodes.Add(node.Url);
            }
        }
        return nodes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Splits user input into node addresses. Accepts spaces, commas, semicolons or line breaks as separators;
    /// a bare "host:port" is read as TCP. Entries that are not addresses are returned in <paramref name="invalid"/>.
    /// </summary>
    public static List<string> ParseNodes(string? text, out List<string> invalid)
    {
        var nodes = new List<string>();
        invalid = [];
        if (string.IsNullOrWhiteSpace(text))
            return nodes;

        char[] separators = [' ', '\t', '\r', '\n', ',', ';', '，', '；', '、'];
        foreach (string part in text.Split(separators, StringSplitOptions.RemoveEmptyEntries))
        {
            string node = part.Contains("://") ? part : "tcp://" + part;
            if (Uri.TryCreate(node, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
                && uri.Scheme is "tcp" or "udp" or "ws" or "wss" or "quic" or "http" or "https" or "txt" or "srv")
                nodes.Add(node);
            else
                invalid.Add(part);
        }
        return nodes;
    }

    private static LinkSettingsData Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var data = JsonSerializer.Deserialize<LinkSettingsData>(File.ReadAllText(FilePath));
                if (data is not null)
                    return data;
            }
        }
        catch (Exception ex)
        {
            LinkLog.Warn("联机设置读取失败,改用默认值:" + ex.Message);
        }
        return new LinkSettingsData();
    }
}
