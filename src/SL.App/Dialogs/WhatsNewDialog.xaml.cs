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
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SL.App.Infrastructure;
using SL.App.Localization;
using SL.App.Themes;

namespace SL.App.Dialogs;

/// <summary>One highlight row in the update announcement.</summary>
public sealed record WhatsNewItem(string Glyph, string Title, string Detail);

/// <summary>
/// Shown once after the launcher version changes: lists what is new and states that user data is preserved.
/// </summary>
public sealed partial class WhatsNewDialog : ContentDialog
{
    public WhatsNewDialog(string version, IReadOnlyList<WhatsNewItem> items)
    {
        InitializeComponent();

        Title = Loc.T("更新公告");
        PrimaryButtonText = Loc.T("知道了");
        VersionText.Text = string.Format(Loc.T("已更新到 {0}"), version);
        SubtitleText.Text = Loc.T("本次更新带来了以下变化，可以随时在「关于」页查看版本信息。");
        FootnoteText.Text = string.Format(Loc.T("想了解全部改动，可打开项目发布页：{0}"), LauncherUpdater.ReleasesUrl);

        foreach (var item in items)
            HighlightsHost.Children.Add(BuildRow(item));
    }

    private static UIElement BuildRow(WhatsNewItem item)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconHost = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(8),
            VerticalAlignment = VerticalAlignment.Top,
            Background = ThemedBrush("Brush.AccentSoft"),
            Child = new FontIcon
            {
                Glyph = item.Glyph,
                FontSize = 15,
                Foreground = ThemedBrush("Brush.Accent"),
            },
        };
        grid.Children.Add(iconHost);

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock
        {
            Text = item.Title,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ThemedBrush("Brush.Fg"),
        });
        text.Children.Add(new TextBlock
        {
            Text = item.Detail,
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemedBrush("Brush.FgDim"),
        });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        return grid;
    }

    private static Brush? ThemedBrush(string key)
    {
        var resources = Application.Current.Resources;
        var dictKey = ThemeManager.IsDark ? "Default" : "Light";
        if (resources.ThemeDictionaries.TryGetValue(dictKey, out var dict)
            && dict is ResourceDictionary rd
            && rd.TryGetValue(key, out var value)
            && value is Brush brush)
            return brush;
        return null;
    }
}
