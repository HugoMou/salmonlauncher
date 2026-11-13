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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using SL.Core.Logging;

namespace SL.App.Infrastructure;

/// <summary>Progress of a ranged download: bytes received, total size, average speed and the source in use.</summary>
public sealed record DownloadProgressInfo(long Received, long Total, double BytesPerSecond, string Source);

/// <summary>One place a file can be fetched from.</summary>
public sealed record DownloadSource(string Url, string Label);

/// <summary>
/// Shared ranged-file downloader used by both the Campfire updater and the launcher self-updater.
/// GitHub throttles a single connection, so the file is fetched as several parallel ranges and each
/// source is tried in turn when a mirror or the direct connection fails.
/// </summary>
public static class RangedDownloader
{
    private const int RangeParts = 6;
    private const long ParallelThreshold = 256 * 1024;
    private const int StallSeconds = 20;

    private static readonly HttpClient DownloadHttp = CreateDownloadClient();

    private static HttpClient CreateDownloadClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            MaxConnectionsPerServer = RangeParts + 2,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        };
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"SalmonLauncher/{Core.Boot.Runtime.AppVersion}");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        return client;
    }

    /// <summary>
    /// Download <paramref name="sources"/> in order into <paramref name="target"/>, verifying the SHA-256 when known.
    /// Returns the number of sources attempted.
    /// </summary>
    public static async Task<int> DownloadAsync(
        IReadOnlyList<DownloadSource> sources,
        long expectedSize,
        string? expectedSha256,
        string target,
        IProgress<DownloadProgressInfo>? progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? AppContext.BaseDirectory);
        Exception? lastError = null;
        var attempted = 0;

        foreach (var source in sources)
        {
            ct.ThrowIfCancellationRequested();
            attempted++;
            try
            {
                AppLog.Info("Download", $"下载源：{source.Label}");
                if (expectedSize >= ParallelThreshold)
                {
                    try
                    {
                        await DownloadRangedAsync(source, expectedSize, target, progress, ct).ConfigureAwait(false);
                    }
                    catch (RangeNotSupportedException)
                    {
                        AppLog.Warn("Download", $"{source.Label} 不支持分段下载，改用单连接");
                        await DownloadSingleAsync(source, expectedSize, target, progress, ct).ConfigureAwait(false);
                    }
                }
                else
                {
                    await DownloadSingleAsync(source, expectedSize, target, progress, ct).ConfigureAwait(false);
                }

                await VerifyAsync(target, expectedSha256, ct).ConfigureAwait(false);
                return attempted;
            }
            catch (OperationCanceledException)
            {
                Cleanup(target);
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                AppLog.Warn("Download", $"{source.Label} 下载失败：{ex.Message}");
                Cleanup(target);
            }
        }

        throw new InvalidOperationException(
            $"所有下载源均失败：{lastError?.Message ?? "未知错误"}", lastError);
    }

    private static async Task DownloadRangedAsync(
        DownloadSource source,
        long total,
        string target,
        IProgress<DownloadProgressInfo>? progress,
        CancellationToken ct)
    {
        var partSize = (total + RangeParts - 1) / RangeParts;
        var paths = new string[RangeParts];
        var received = 0L;
        var started = Stopwatch.StartNew();
        var lastReport = 0L;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var gate = new object();
        Exception? failure = null;

        void Report(bool force)
        {
            if (progress is null)
                return;
            var elapsed = started.ElapsedMilliseconds;
            lock (gate)
            {
                if (!force && elapsed - lastReport < 200)
                    return;
                lastReport = elapsed;
                var speed = elapsed > 0 ? Interlocked.Read(ref received) * 1000d / elapsed : 0;
                progress.Report(new DownloadProgressInfo(
                    Interlocked.Read(ref received), total, speed, source.Label));
            }
        }

        async Task DownloadPartAsync(int index)
        {
            try
            {
                var from = index * partSize;
                var to = Math.Min(from + partSize - 1, total - 1);
                var expected = to - from + 1;
                var path = paths[index] = $"{target}.part{index}";

                using var request = new HttpRequestMessage(HttpMethod.Get, source.Url)
                {
                    Version = HttpVersion.Version11,
                    VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                };
                request.Headers.Range = new RangeHeaderValue(from, to);

                using var response = await DownloadHttp
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                    .ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.OK)
                    throw new RangeNotSupportedException("服务器返回了完整文件（不支持 Range）");
                if (response.StatusCode != HttpStatusCode.PartialContent)
                    throw new HttpRequestException($"HTTP {(int)response.StatusCode}");

                var declared = response.Content.Headers.ContentLength;
                if (declared is > 0 && declared != expected)
                    throw new IOException($"分段大小不符：应为 {expected}，实际 {declared}");

                await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize: 64 * 1024, useAsync: true);
                await using var input = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);

                var buffer = new byte[64 * 1024];
                long partReceived = 0;
                while (true)
                {
                    var read = await ReadWithStallAsync(input, buffer, linked.Token).ConfigureAwait(false);
                    if (read == 0)
                        break;

                    await output.WriteAsync(buffer.AsMemory(0, read), linked.Token).ConfigureAwait(false);
                    partReceived += read;
                    Interlocked.Add(ref received, read);
                    Report(force: false);
                }

                if (partReceived != expected)
                    throw new IOException($"分段下载不完整：应为 {expected}，实际 {partReceived}");

                await output.FlushAsync(linked.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested && ex is not OperationCanceledException)
                {
                    lock (gate)
                        failure ??= ex;
                }
                linked.Cancel();
                throw;
            }
        }

        try
        {
            await Task.WhenAll(Enumerable.Range(0, RangeParts).Select(DownloadPartAsync)).ConfigureAwait(false);
            Report(force: true);

            await using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None,
                             bufferSize: 64 * 1024, useAsync: true))
            {
                foreach (var part in paths)
                {
                    await using var input = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read,
                        bufferSize: 64 * 1024, useAsync: true);
                    await input.CopyToAsync(output, 64 * 1024, ct).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            linked.Cancel();
            Exception? captured;
            lock (gate)
                captured = failure;
            if (captured is not null)
                throw captured;
            throw;
        }
        finally
        {
            foreach (var part in paths)
                TryDelete(part);
        }
    }

    private static async Task DownloadSingleAsync(
        DownloadSource source,
        long expectedSize,
        string target,
        IProgress<DownloadProgressInfo>? progress,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, source.Url)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
        using var response = await DownloadHttp
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? expectedSize;
        if (total <= 0)
            total = expectedSize;

        await using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 64 * 1024, useAsync: true);
        await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        var buffer = new byte[64 * 1024];
        var started = Stopwatch.StartNew();
        long received = 0;
        var lastReport = 0L;
        while (true)
        {
            var read = await ReadWithStallAsync(input, buffer, ct).ConfigureAwait(false);
            if (read == 0)
                break;

            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            received += read;

            if (progress is not null && started.ElapsedMilliseconds - lastReport >= 200)
            {
                lastReport = started.ElapsedMilliseconds;
                var speed = lastReport > 0 ? received * 1000d / lastReport : 0;
                progress.Report(new DownloadProgressInfo(received, total, speed, source.Label));
            }
        }

        await output.FlushAsync(ct).ConfigureAwait(false);
        progress?.Report(new DownloadProgressInfo(received, total,
            started.Elapsed.TotalSeconds > 0 ? received / started.Elapsed.TotalSeconds : 0, source.Label));
    }

    private static async Task<int> ReadWithStallAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        using var stall = new CancellationTokenSource(TimeSpan.FromSeconds(StallSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, stall.Token);
        try
        {
            return await stream.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"{StallSeconds} 秒没有收到数据");
        }
    }

    /// <summary>Verify a downloaded file against the published SHA-256 (skipped when unknown).</summary>
    public static async Task VerifyAsync(string path, string? expectedSha256, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
            return;

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, useAsync: true);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false))
            .ToLowerInvariant();
        if (!actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"文件校验失败：应为 {expectedSha256}，实际为 {actual}。");
    }

    private static void Cleanup(string target)
    {
        TryDelete(target);
        for (var i = 0; i < RangeParts; i++)
            TryDelete($"{target}.part{i}");
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Leftover files are harmless; they are overwritten on the next attempt.
        }
    }

    private sealed class RangeNotSupportedException(string message) : Exception(message);
}
