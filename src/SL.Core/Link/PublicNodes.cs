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
namespace SL.Core.Link;

/// <summary>One built-in public node. The UI shows its region and provider, never its address.</summary>
public sealed class PublicNode
{
    public PublicNode(string id, string url, string provider, string region, bool limited = false)
    {
        Id = id;
        Url = url;
        Provider = provider;
        Region = region;
        IsLimited = limited;
    }

    /// <summary>Stable name used in settings.</summary>
    public string Id { get; }

    /// <summary>EasyTier peer address, e.g. <c>tcp://host:11010</c>.</summary>
    public string Url { get; }

    /// <summary>Who runs the node.</summary>
    public string Provider { get; }

    /// <summary>Rough region the node sits in.</summary>
    public string Region { get; }

    /// <summary>Whether the provider caps relayed traffic.</summary>
    public bool IsLimited { get; }
}

/// <summary>The built-in public nodes, used by default to help two peers find each other.</summary>
public static class PublicNodes
{
    /// <summary>Community nodes that relay traffic, from several providers (EasyTier port 11010).</summary>
    public static IReadOnlyList<PublicNode> All { get; } =
    [
        new PublicNode("sh-ct", "tcp://225284.xyz:11010", "海波", "上海·电信"),
        new PublicNode("xm-ct", "tcp://easytier.weiai.org.cn:11010", "为爱，唯爱", "厦门·电信"),
        new PublicNode("cq-cm", "tcp://183.230.36.171:11010", "Just the Two of Us", "重庆·移动"),
        new PublicNode("zz-bgp", "udp://et.basd1.de:11010", "宇智波.希", "郑州·BGP"),
        new PublicNode("sh-bgp", "tcp://et.sumnight.top:11010", "夏夜", "上海·BGP", limited: true),
        new PublicNode("cn-eo", "wss://et.chinokou.cn", "ChinoKou", "全国·EdgeOne"),
        new PublicNode("yn-cu", "https://et-public.lctn.top", "凉城听暖", "云南·联通"),
        new PublicNode("intl-cf", "wss://et.xn--6kr850c.com", "林然", "国际·Cloudflare"),
        new PublicNode("intl-wk", "https://etnode.zkitefly.eu.org/node2", "zkitefly", "国际·Workers"),
    ];

    public static PublicNode? ById(string id) => All.FirstOrDefault(n => n.Id == id);
}
