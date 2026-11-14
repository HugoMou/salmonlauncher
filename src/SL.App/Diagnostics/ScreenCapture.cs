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
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SL.App.Infrastructure;
using SL.App.Navigation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace SL.App.Diagnostics;

/// <summary>
/// Headless screenshot pass used to produce documentation and store artwork.
/// Started with <c>--capture &lt;folder&gt;</c>: the launcher walks every registered page,
/// renders the live visual tree to a PNG, and exits. Because the frames come from the
/// render tree itself, what is on top of the window on the desktop is irrelevant.
/// </summary>
public static class ScreenCapture
{
    /// <summary>Folder requested on the command line, or null when capture mode is off.</summary>
    public static string? RequestedFolder { get; } = ParseValue("--capture");

    /// <summary>When set, only this page is captured before exiting.</summary>
    public static string? RequestedPage { get; } = ParseValue("--capture-page");

    public static bool IsEnabled => RequestedFolder is not null;

    private static string? ParseValue(string flag)
    {
        try
        {
            var argv = Environment.GetCommandLineArgs();
            for (var i = 0; i < argv.Length; i++)
            {
                var arg = argv[i];
                string? value = null;

                if (arg.Equals(flag, StringComparison.OrdinalIgnoreCase))
                    value = i + 1 < argv.Length ? argv[i + 1] : null;
                else if (arg.StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase))
                    value = arg[(flag.Length + 1)..];

                if (value is null)
                    continue;

                var trimmed = value.Trim().Trim('"');
                return trimmed.Length == 0 ? null : trimmed;
            }
        }
        catch (Exception ex)
        {
            Core.Logging.AppLog.Warn("Capture", $"解析 {flag} 失败:" + ex.Message);
        }
        return null;
    }

    /// <summary>
    /// Navigate through every catalogued page, saving one PNG each, then returns. The caller
    /// exits the process afterwards.
    /// </summary>
    public static async Task RunAsync(FrameworkElement host, string folder)
    {
        Directory.CreateDirectory(folder);

        // Single page mode: one launch captures one page, which keeps the live-tree render away
        // from any accumulated state. Repeated RenderAsync calls on the same window proved able to
        // wedge the UI thread, so the batch below is only used for a deliberate full pass.
        if (RequestedPage is { } single)
        {
            await CaptureOneAsync(host, folder, single, 1);
            return;
        }

        // Pages that only render meaningfully once an instance exists are still captured: they
        // show the honest empty state rather than a fabricated one.
        var order = new[]
        {
            "launch.home", "launch.tasks",
            "inst.overview", "inst.mods", "inst.saves", "inst.cfg", "inst.logs",
            "dl.game", "dl.loaders", "dl.install",
            "dl.pack", "dl.shaders", "dl.packs",
            "inst.packs", "inst.shaders",
            "tools.online", "tools.skins", "tools.diag", "tools.log",
            "st.global", "st.launch", "st.theme", "st.about", "st.terms",
        };

        var index = 0;
        foreach (var key in order)
        {
            if (!PageCatalog.All.ContainsKey(key))
                continue;

            index++;
            await CaptureOneAsync(host, folder, key, index);
        }

        Core.Logging.AppLog.Info("Capture", $"完成，共 {index} 页 → {folder}");
    }

    /// <summary>Navigate to one page, let it settle, and write a single PNG.</summary>
    private static async Task CaptureOneAsync(FrameworkElement host, string folder, string key, int index)
    {
        try
        {
            AppServices.Nav.Navigate(key);

            // Give the page a moment for data loads and layout to settle; a still taken too
            // early catches placeholder text instead of the real content.
            await Task.Delay(1100);

            var name = $"page-{index:d2}-{key.Replace('.', '-')}.png";
            var saved = await CaptureAsync(host, Path.Combine(folder, name));
            Core.Logging.AppLog.Info("Capture", $"{(saved ? "saved" : "failed")} {name}");
        }
        catch (Exception ex)
        {
            Core.Logging.AppLog.Warn("Capture", $"{key}: {ex.Message}");
        }
    }

    /// <summary>Render one element to a PNG file. Returns false when the frame could not be written.</summary>
    public static async Task<bool> CaptureAsync(UIElement element, string path)
    {
        try
        {
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(element);

            var pixels = await bitmap.GetPixelsAsync();
            var width = (uint)bitmap.PixelWidth;
            var height = (uint)bitmap.PixelHeight;
            if (width == 0 || height == 0)
                return false;

            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                width,
                height,
                96,
                96,
                pixels.ToArray());
            await encoder.FlushAsync();

            stream.Seek(0);
            using var file = File.Create(path);
            await stream.AsStreamForRead().CopyToAsync(file);
            return true;
        }
        catch (Exception ex)
        {
            Core.Logging.AppLog.Warn("Capture", "渲染失败:" + ex.Message);
            return false;
        }
    }
}