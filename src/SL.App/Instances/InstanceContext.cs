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
using SL.App.Infrastructure;
using SL.Core.Instances;

namespace SL.App.Instances;

/// <summary>
/// The single "current instance" shared by the launch panel and every instance page.
/// It is persisted, so picking an instance on one page is honoured everywhere and survives a restart.
/// </summary>
public static class InstanceContext
{
    private static bool _loaded;

    public static string CurrentId { get; private set; } = "";

    public static event Action? Changed;

    public static IReadOnlyList<InstanceInfo> All => InstanceRegistry.List();

    /// <summary>Restore the persisted choice once, falling back to the newest instance.</summary>
    private static void EnsureLoaded()
    {
        if (_loaded)
            return;
        _loaded = true;
        CurrentId = AppServices.Settings.Current.ActiveInstanceId ?? "";
    }

    /// <summary>Refresh the instance list and keep CurrentId valid (falls back to the newest instance when stale).</summary>
    public static IReadOnlyList<InstanceInfo> Refresh()
    {
        EnsureLoaded();
        var all = InstanceRegistry.List();
        if (all.All(i => i.InstanceId != CurrentId))
        {
            CurrentId = all.Count > 0 ? all[0].InstanceId : "";
            Persist();
            Changed?.Invoke();
        }
        return all;
    }

    public static void Select(string instanceId)
    {
        EnsureLoaded();
        if (CurrentId == instanceId)
            return;
        CurrentId = instanceId;
        Persist();
        Changed?.Invoke();
    }

    private static void Persist()
    {
        var id = CurrentId;
        try
        {
            AppServices.Settings.Update(c => c.ActiveInstanceId = id);
        }
        catch
        {
            // The selection is still valid in memory even if writing the settings file fails.
        }
    }
}