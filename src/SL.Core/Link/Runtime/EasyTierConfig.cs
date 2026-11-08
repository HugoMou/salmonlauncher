// ---------------------------------------------------------------------------
// Ported from Campfire (https://github.com/Xocio/Campfire), branch main, 2026-11-08.
// Copyright (C) the Campfire contributors. Licensed under GPL-3.0.
// Adapted for SalmonLauncher: namespace, settings/logging references, and the two
// user-facing error messages. See THIRD-PARTY-NOTICES.md for the full statement.
// ---------------------------------------------------------------------------
#nullable disable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SL.Core.Link.Runtime
{
    /// <summary>
    /// Settings of one virtual network instance, written to a TOML file.
    /// </summary>
    /// <remarks>
    /// The instance is started from this file and accepts port forwards while running.
    /// </remarks>
    public sealed class EasyTierConfig
    {
        public string NetworkName { get; set; }

        public string NetworkSecret { get; set; }

        public string Hostname { get; set; }

        /// <summary>A fixed virtual address, or <c>null</c> to get one automatically.</summary>
        public string IPv4 { get; set; }

        /// <summary>
        /// SalmonLauncher addition: upstream always runs without a TUN device and forwards ports instead.
        /// Our room service is reached by virtual address, so the adapter stays on unless a caller says otherwise.
        /// </summary>
        public bool NoTun { get; set; } = false;

        public List<string> Peers { get; } = new List<string>();

        /// <summary>Ports other members may reach on this computer. "0" alone blocks every incoming connection.</summary>
        public List<string> TcpWhitelist { get; } = new List<string>();

        public List<string> UdpWhitelist { get; } = new List<string>();

        /// <summary>When set, traffic only flows over direct connections and never through a relay node.</summary>
        public bool DirectOnly { get; set; }

        /// <summary>
        /// When set, this computer never connects directly to other members and does not listen for them; traffic
        /// goes through relay nodes.
        /// </summary>
        public bool HideAddress { get; set; }

        public string ToToml()
        {
            var toml = new StringBuilder();
            toml.AppendLine("hostname = " + Quote(Hostname));
            if (!string.IsNullOrEmpty(IPv4))
                toml.AppendLine("ipv4 = " + Quote(IPv4));
            else
                toml.AppendLine("dhcp = true");

            // Random ports on every interface (IPv6 is added automatically); none when the address is hidden.
            toml.AppendLine(HideAddress ? "listeners = []" : "listeners = [\"tcp://0.0.0.0:0\", \"udp://0.0.0.0:0\"]");
            toml.AppendLine("tcp_whitelist = " + Array(TcpWhitelist));
            toml.AppendLine("udp_whitelist = " + Array(UdpWhitelist));
            toml.AppendLine();

            toml.AppendLine("[network_identity]");
            toml.AppendLine("network_name = " + Quote(NetworkName));
            toml.AppendLine("network_secret = " + Quote(NetworkSecret));
            toml.AppendLine();

            foreach (string peer in Peers)
            {
                toml.AppendLine("[[peer]]");
                toml.AppendLine("uri = " + Quote(peer));
                toml.AppendLine();
            }

            toml.AppendLine("[flags]");
            toml.AppendLine(NoTun ? "no_tun = true" : "no_tun = false");
            toml.AppendLine("latency_first = true");
            toml.AppendLine("enable_kcp_proxy = true");
            if (EasyTierPackage.IsWindows7)
            {
                // On Windows 7 traffic uses TCP and KCP, not QUIC.
                toml.AppendLine("enable_quic_proxy = false");
                toml.AppendLine("disable_quic_input = true");
            }
            else
            {
                toml.AppendLine("enable_quic_proxy = true");
            }
            // Zstandard compression.
            toml.AppendLine("data_compress_algo = 2");
            // Never act as a relay for strangers' networks.
            toml.AppendLine("private_mode = true");
            toml.AppendLine("p2p_only = " + (DirectOnly && !HideAddress ? "true" : "false"));
            if (HideAddress)
            {
                toml.AppendLine("disable_p2p = true");
                toml.AppendLine("disable_udp_hole_punching = true");
                toml.AppendLine("disable_tcp_hole_punching = true");
                toml.AppendLine("disable_sym_hole_punching = true");
                toml.AppendLine("disable_upnp = true");
            }
            return toml.ToString();
        }

        private static string Array(IEnumerable<string> values)
        {
            return "[" + string.Join(", ", values.Select(Quote)) + "]";
        }

        private static string Quote(string value)
        {
            var quoted = new StringBuilder("\"");
            foreach (char c in value ?? "")
            {
                if (c == '"' || c == '\\')
                    quoted.Append('\\').Append(c);
                else if (c < 0x20)
                    quoted.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                else
                    quoted.Append(c);
            }
            return quoted.Append('"').ToString();
        }
    }
}
