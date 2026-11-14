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
using Microsoft.UI.Xaml.Navigation;

namespace SL.App.Pages;

/// <summary>Launch dashboard home: large launch button, instance selection, account status, and shortcuts.</summary>
public sealed partial class HomePage : Page
{
    private readonly HomeVM _vm = new();

    public HomePage()
    {
        InitializeComponent();
        Root.DataContext = _vm;
        VersionBadgeText.Text = $"v{SL.Core.Boot.Runtime.AppVersion}";
    }

    /// <summary>Picking an instance on the launch panel updates the shared instance used by every instance page.</summary>
    private void OnInstanceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm.SelectedInstance is { } row)
            SL.App.Instances.InstanceContext.Select(row.Id);
    }

    /// <summary>Open the selected instance's folder in Explorer; jumps to the instances root when none is selected.</summary>
    private void OnOpenInstanceFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            var root = SL.Core.Boot.Runtime.InstancesDir;
            var target = _vm.SelectedInstance is { } row
                ? SL.Core.Instances.InstanceRegistry.InstanceDirOf(row.Id)
                : root;

            if (!Directory.Exists(target))
                target = root;

            Directory.CreateDirectory(target);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{target}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Core.Logging.AppLog.Warn("Home", "打开实例目录失败:" + ex.Message);
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _vm.Refresh();
    }
}
