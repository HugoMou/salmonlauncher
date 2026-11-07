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

namespace SL.Core.Link.Room;

/// <summary>How a player is signed in to Minecraft, as far as the room needs to know.</summary>
public enum AccountKind
{
    Unknown,
    Offline,
    Microsoft,
}

/// <summary>One player as the room shows them.</summary>
public sealed class PlayerProfile
{
    private PlayerProfile() { }

    public PlayerProfile(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>Stable identifier of the player (the machine id of the sender).</summary>
    public string Id { get; private set; } = "";

    /// <summary>Name shown in the room list.</summary>
    public string Name { get; private set; } = "";

    public AccountKind Account { get; private set; }

    /// <summary>Name of the sender's program, when it chose to share it.</summary>
    public string Vendor { get; private set; } = "";

    /// <summary>Version of the sender's program, when shared.</summary>
    public string VendorVersion { get; private set; } = "";

    /// <summary>Minecraft version and loader of the sender's game, when known.</summary>
    public string GameVersion { get; private set; } = "";

    /// <summary>Account id used to draw the skin face, shared only when the sender allows it.</summary>
    public string Skin { get; private set; } = "";

    /// <summary>True for the host of the room.</summary>
    public bool IsHost { get; private set; }

    public PlayerProfile WithAccount(AccountKind kind) { Account = kind; return this; }
    public PlayerProfile WithVendor(string name, string version) { Vendor = name; VendorVersion = version; return this; }
    public PlayerProfile WithGame(string version) { GameVersion = version; return this; }
    public PlayerProfile WithSkin(string skin) { Skin = skin; return this; }
    public PlayerProfile AsHost(bool host = true) { IsHost = host; return this; }

    /// <summary>Serialises the profile for the room service, optionally tagging it as a host entry.</summary>
    public byte[] ToJson(bool includeKind = true)
    {
        var wire = new WireProfile(Id, Name, Account.ToString(), Vendor, VendorVersion, GameVersion, Skin, IsHost, includeKind);
        return JsonSerializer.SerializeToUtf8Bytes(wire, LinkJson.Options);
    }

    /// <summary>Reads a profile; returns <c>null</c> when it is not usable.</summary>
    public static PlayerProfile? FromJson(JsonElement element, string? forcedId)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        string id = forcedId ?? Text(element, "id");
        string name = Text(element, "name");
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
            return null;

        var profile = new PlayerProfile(id, name);
        profile.Account = Enum.TryParse<AccountKind>(Text(element, "account"), ignoreCase: true, out var kind) ? kind : AccountKind.Unknown;
        profile.Vendor = Text(element, "vendor");
        profile.VendorVersion = Text(element, "vendorVersion");
        profile.GameVersion = Text(element, "game");
        profile.Skin = Text(element, "skin");
        profile.IsHost = element.TryGetProperty("host", out var h) && h.ValueKind == JsonValueKind.True;
        return profile;
    }

    private static string Text(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private sealed record WireProfile(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("account")] string Account,
        [property: JsonPropertyName("vendor")] string Vendor,
        [property: JsonPropertyName("vendorVersion")] string VendorVersion,
        [property: JsonPropertyName("game")] string Game,
        [property: JsonPropertyName("skin")] string Skin,
        [property: JsonPropertyName("host")] bool Host,
        [property: JsonPropertyName("kind")] bool Kind);
}

/// <summary>Shared JSON options so every link payload is written the same way.</summary>
internal static class LinkJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };
}
