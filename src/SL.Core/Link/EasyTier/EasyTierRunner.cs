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
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using SL.Core.Link.Room;

namespace SL.Core.Link.EasyTier;

/// <summary>Command line for the EasyTier core, one builder per side of a room.</summary>
public static class EasyTierArguments
{
    /// <summary>Where the host holds its address inside the room network; guests always find it there.</summary>
    public const string HostAddress = "10.144.144.1";

    /// <summary>The host keeps a fixed address so a guest never has to look anything up.</summary>
    public static List<string> Host(RoomCode code, int servicePort, IEnumerable<string> peers)
    {
        var arguments = Common(code, peers);
        arguments.AddRange(["-i", HostAddress]);
        arguments.AddRange(["--hostname", RoomProtocol.HostNamePrefix + servicePort]);
        return arguments;
    }

    /// <summary>A guest takes whatever address inside the room network the host hands out.</summary>
    public static List<string> Guest(RoomCode code, IEnumerable<string> peers)
    {
        var arguments = Common(code, peers);
        arguments.Add("-d");
        return arguments;
    }

    private static List<string> Common(RoomCode code, IEnumerable<string> peers)
    {
        var arguments = new List<string>
        {
            "--network-name", code.NetworkName,
            "--network-secret", code.NetworkSecret,
            "--console-log-level", "info",
            "--multi-thread",
        };

        foreach (string peer in peers)
        {
            arguments.Add("-p");
            arguments.Add(peer);
        }
        return arguments;
    }
}

/// <summary>
/// One running EasyTier core process. The core creates a TUN adapter, which on Windows needs an elevated
/// launcher, so <see cref="EasyTierRunner.Start"/> is only called after the privilege check in the caller.
/// </summary>
public sealed class EasyTierRunner : IDisposable
{
    private readonly Process _process;

    private EasyTierRunner(Process process) => _process = process;

    /// <summary>Every line the core prints, already trimmed (thread-pool thread).</summary>
    public event Action<string>? Line;

    /// <summary>Exit code, raised once when the core stops on its own.</summary>
    public event Action<int>? Exited;

    public bool Running
    {
        get
        {
            try
            {
                return !_process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    /// <summary>Starts the core; throws <see cref="LinkException"/> when it cannot even be launched.</summary>
    public static EasyTierRunner Start(string corePath, IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(corePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(corePath) ?? LinkSettings.RuntimeDirectory,
        };
        foreach (string argument in arguments)
            info.ArgumentList.Add(argument);

        Process process;
        try
        {
            process = Process.Start(info) ?? throw new LinkException("联机内核未能启动");
        }
        catch (Exception ex) when (ex is not LinkException)
        {
            throw new LinkException("联机内核未能启动:" + ex.Message, ex);
        }

        var runner = new EasyTierRunner(process);
        process.OutputDataReceived += (_, e) => runner.Publish(e.Data);
        process.ErrorDataReceived += (_, e) => runner.Publish(e.Data);
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) =>
        {
            try { runner.Exited?.Invoke(process.ExitCode); } catch { }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        LinkLog.Info("联机内核已启动(PID " + process.Id + "):" + string.Join(' ', arguments));
        return runner;
    }

    private void Publish(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        try { Line?.Invoke(text.Trim()); } catch { }
    }

    /// <summary>Stops the core and everything it started (the TUN adapter goes away with it).</summary>
    public void Stop()
    {
        try
        {
            if (_process.HasExited)
                return;
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            LinkLog.Warn("停止联机内核时出错:" + ex.Message);
        }
    }

    /// <summary>
    /// Waits until something accepts a TCP connection at <paramref name="endpoint"/>, which is how a guest
    /// knows the virtual network is up and the host's room service can be reached.
    /// </summary>
    public static async Task<bool> WaitForTcpAsync(System.Net.IPEndPoint endpoint, TimeSpan timeout, CancellationToken token)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            using (var probe = new TcpClient())
            {
                try
                {
                    using var attempt = CancellationTokenSource.CreateLinkedTokenSource(token);
                    attempt.CancelAfter(TimeSpan.FromSeconds(2));
                    await probe.ConnectAsync(endpoint.Address, endpoint.Port, attempt.Token).ConfigureAwait(false);
                    return true;
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    // not up yet
                }
                catch (SocketException)
                {
                    // not up yet
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
        return false;
    }

    public void Dispose()
    {
        Stop();
        _process.Dispose();
    }
}
