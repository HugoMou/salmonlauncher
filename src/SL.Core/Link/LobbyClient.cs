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
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SL.Core.Link;

/// <summary>One room as the lobby lists it. <see cref="Code"/> is empty when the room is password protected.</summary>
public sealed record LobbyRoom(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("notice")] string Notice,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("players")] int Players,
    [property: JsonPropertyName("maxPlayers")] int MaxPlayers,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("hasPassword")] bool HasPassword,
    [property: JsonPropertyName("code")] string Code);

/// <summary>What the host hands to the lobby so others can find the room.</summary>
public sealed record LobbyOffer(
    string Code,
    string Name,
    string Notice,
    IReadOnlyList<string> Tags,
    int Players,
    int MaxPlayers,
    string Version,
    string Password,
    string Secret,
    int TtlSeconds);

/// <summary>
/// Talks to a self-hosted lobby: a tiny HTTP service that lists rooms and, for a password protected one,
/// only hands out the room code to whoever knows the password. The service never sees the password, only its
/// hash, and a room disappears by itself when the host stops refreshing it.
/// </summary>
public static class LobbyClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>Address of the lobby service, without a trailing slash.</summary>
    public static string BaseUrl
    {
        get
        {
            string configured = LinkSettings.Current.LobbyUrl.Trim();
            if (configured.Length == 0)
                configured = DefaultUrl;
            return configured.TrimEnd('/');
        }
    }

    /// <summary>Used when the player has not pointed the launcher at their own lobby.</summary>
    public const string DefaultUrl = "https://sl.hugomou.com/lobby";

    /// <summary>Publishes or refreshes a room; the same secret keeps updating the same entry.</summary>
    public static async Task<string> PublishAsync(LobbyOffer offer, CancellationToken token)
    {
        var payload = new Dictionary<string, object?>
        {
            ["code"] = offer.Code,
            ["name"] = offer.Name,
            ["notice"] = offer.Notice,
            ["tags"] = offer.Tags,
            ["players"] = offer.Players,
            ["maxPlayers"] = offer.MaxPlayers,
            ["version"] = offer.Version,
            ["passwordHash"] = offer.Password.Length > 0 ? HashPassword(offer.Password) : "",
            ["secret"] = offer.Secret,
            ["ttl"] = offer.TtlSeconds,
        };

        using var response = await Http.PostAsJsonAsync(BaseUrl + "/rooms", payload, token).ConfigureAwait(false);
        await EnsureOkAsync(response, "发布到大厅失败").ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync<IdReply>(cancellationToken: token).ConfigureAwait(false);
        return result?.Id ?? "";
    }

    /// <summary>Every room the lobby currently knows about.</summary>
    public static async Task<IReadOnlyList<LobbyRoom>> ListAsync(CancellationToken token)
    {
        using var response = await Http.GetAsync(BaseUrl + "/rooms", token).ConfigureAwait(false);
        await EnsureOkAsync(response, "读取大厅失败").ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync<ListReply>(cancellationToken: token).ConfigureAwait(false);
        return result?.Rooms ?? [];
    }

    /// <summary>Asks a password protected room for its code; returns <c>null</c> when the password is wrong.</summary>
    public static async Task<string?> RevealAsync(string id, string password, CancellationToken token)
    {
        var payload = new Dictionary<string, object?> { ["passwordHash"] = HashPassword(password) };
        using var response = await Http.PostAsJsonAsync($"{BaseUrl}/rooms/{Uri.EscapeDataString(id)}/reveal", payload, token)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return null;
        await EnsureOkAsync(response, "获取房间码失败").ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync<CodeReply>(cancellationToken: token).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(result?.Code) ? null : result!.Code;
    }

    /// <summary>Removes a room from the lobby; only the secret used to publish it may do this.</summary>
    public static async Task UnpublishAsync(string id, string secret, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/rooms/{Uri.EscapeDataString(id)}");
        request.Headers.Add("X-Room-Secret", secret);
        using var response = await Http.SendAsync(request, token).ConfigureAwait(false);
        // a room that already expired is as good as removed
        if (response.StatusCode != HttpStatusCode.NotFound)
            await EnsureOkAsync(response, "取消发布失败").ConfigureAwait(false);
    }

    /// <summary>SHA-256 of the password, lower-case hex: the service stores nothing reversible.</summary>
    public static string HashPassword(string password)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    private static async Task EnsureOkAsync(HttpResponseMessage response, string what)
    {
        if (response.IsSuccessStatusCode)
            return;

        string body = "";
        try { body = await response.Content.ReadAsStringAsync().ConfigureAwait(false); } catch { }
        string detail = body.Length > 200 ? body[..200] : body;
        throw new LinkException(string.IsNullOrWhiteSpace(detail) ? $"{what}(HTTP {(int)response.StatusCode})" : $"{what}:{detail}");
    }

    private sealed record IdReply([property: JsonPropertyName("id")] string? Id);

    private sealed record ListReply([property: JsonPropertyName("rooms")] List<LobbyRoom>? Rooms);

    private sealed record CodeReply([property: JsonPropertyName("code")] string? Code);
}
