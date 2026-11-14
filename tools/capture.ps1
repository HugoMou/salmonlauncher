# SalmonLauncher (SL) — promotional screenshot capture
# Copyright (C) 2026 Hugo Mou — GPL-3.0-or-later
#
# Captures the launcher window itself via PrintWindow, so whatever is stacked on top
# of it on the desktop does not matter. Every image is the real application.

param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [switch]$SkipOnboarding
)

$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

$src = @'
using System;
using System.Runtime.InteropServices;
public static class PWin {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
if (-not ('PWin' -as [type])) { Add-Type -TypeDefinition $src }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Get-AppWindow {
    param([int]$Id)

    # Resolve from the process's own HWND first: searching the desktop root by process id is
    # unreliable while a WinUI window is still laying out.
    for ($i = 0; $i -lt 10; $i++) {
        $proc = Get-Process -Id $Id -ErrorAction SilentlyContinue
        if ($proc -and $proc.MainWindowHandle -ne [IntPtr]::Zero) {
            try {
                $el = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
                if ($el) { return $el }
            }
            catch { }
        }
        Start-Sleep -Milliseconds 300
    }

    # Fall back to a desktop search if the handle route never yielded an element.
    $rootEl = [System.Windows.Automation.AutomationElement]::RootElement
    for ($i = 0; $i -lt 8; $i++) {
        try {
            $cond = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $Id)
            $w = $rootEl.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
            if ($w) { return $w }
        }
        catch { }
        Start-Sleep -Milliseconds 400
    }
    return $null
}

# PW_RENDERFULLCONTENT (2) grabs the window's own pixels even when covered.
function Save-Shot {
    param([IntPtr]$Handle, [string]$Path)
    $r = New-Object PWin+RECT
    [PWin]::GetWindowRect($Handle, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left
    $h = $r.Bottom - $r.Top
    if ($w -le 0 -or $h -le 0) { return $false }

    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    $ok = [PWin]::PrintWindow($Handle, $hdc, 2)
    $g.ReleaseHdc($hdc)
    $g.Dispose()
    if (-not $ok) { $bmp.Dispose(); return $false }
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return $true
}

function Find-Button {
    param($Root, [string]$Name)
    $c1 = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $c2 = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $and = New-Object System.Windows.Automation.AndCondition($c1, $c2)
    return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $and)
}

function Get-NavView {
    param($Root)
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'NavView')
    try { return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond) }
    catch { return $null }
}

function Invoke-Nav {
    param($Root, [string]$Label)

    # Search inside NavView only. Walking the whole window subtree is what made the earlier
    # runs time out; the pane holds a couple of dozen items at most.
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $Label)

    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        try {
            $nav = Get-NavView -Root $Root
            $scope = if ($nav) { $nav } else { $Root }
            $items = $scope.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
            for ($i = 0; $i -lt $items.Count; $i++) {
                $el = $items.Item($i)
                if ($el.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) {
                    $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
                    return $true
                }
            }
            return $false
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }
    return $false
}

$proc = Get-Process -Id $ProcessId
$hwnd = $proc.MainWindowHandle
if ([PWin]::IsIconic($hwnd)) { [PWin]::ShowWindow($hwnd, 9) | Out-Null }
Start-Sleep -Milliseconds 600
[PWin]::SetWindowPos($hwnd, [IntPtr]::Zero, 0, 0, 1600, 1000, 0x0040) | Out-Null
[PWin]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 900

$win = Get-AppWindow -Id $ProcessId
if (-not $win) { throw 'window not found' }

if (-not $SkipOnboarding) {
    $files = @('01-welcome-language', '02-welcome-terms', '03-welcome-migrate', '04-welcome-account', '05-welcome-theme')

    if (Find-Button -Root $win -Name '下一步') {
        Save-Shot -Handle $hwnd -Path (Join-Path $OutDir "$($files[0]).png") | Out-Null
        'captured ' + $files[0]

        for ($step = 0; $step -lt 5; $step++) {
            if ($step -eq 1) {
                $cbCond = New-Object System.Windows.Automation.PropertyCondition(
                    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                    [System.Windows.Automation.ControlType]::CheckBox)
                $cb = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cbCond)
                if ($cb) { try { $cb.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle() } catch { }; Start-Sleep -Milliseconds 500 }
            }

            # The migrate and theme steps offer 跳过 alongside 下一步; the migrate scan can leave
            # 下一步 disabled for a while, and the theme step finishes with 开始使用.
            $btn = Find-Button -Root $win -Name '下一步'
            if (-not $btn -or -not $btn.Current.IsEnabled) {
                $skip = Find-Button -Root $win -Name '跳过'
                if ($skip -and $skip.Current.IsEnabled) { $btn = $skip }
            }
            if (-not $btn -or -not $btn.Current.IsEnabled) {
                $start = Find-Button -Root $win -Name '开始使用'
                if ($start -and $start.Current.IsEnabled) { $btn = $start }
            }
            if (-not $btn) { "no advance button at step $step"; break }
            if (-not $btn.Current.IsEnabled) { "advance disabled at step $step"; break }

            try { $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
            catch { "invoke failed at step $step"; break }

            Start-Sleep -Milliseconds 1000
            if ($step + 1 -lt $files.Count) {
                Save-Shot -Handle $hwnd -Path (Join-Path $OutDir "$($files[$step + 1]).png") | Out-Null
                'captured ' + $files[$step + 1]
            }
        }
        Start-Sleep -Milliseconds 1500
    }

    $dlg = Find-Button -Root $win -Name '同意并继续'
    if ($dlg) {
        try { $dlg.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { }
        Start-Sleep -Milliseconds 1200
    }
}

$pages = @(
    @('启动面板', '10-home'),
    @('任务中心', '11-tasks'),
    @('概览', '12-instance-overview'),
    @('模组管理', '13-mods'),
    @('存档', '14-saves'),
    @('实例设置', '15-instance-settings'),
    @('实例日志', '16-instance-logs'),
    @('游戏版本', '20-versions'),
    @('加载器选择', '21-loaders'),
    @('模组仓库', '23-modrinth'),
    @('光影仓库', '24-shaders'),
    @('资源包仓库', '25-resourcepacks'),
    @('资源包', '26-instance-packs'),
    @('光影', '27-instance-shaders'),
    @('联机', '30-multiplayer'),
    @('全局偏好', '40-global-settings'),
    @('崩溃诊断', '41-crash'),
    @('应用日志', '42-logs'),
    @('外观与设置', '50-settings'),
    @('皮肤站', '51-skins'),
    @('关于', '52-about'),
    @('用户协议', '53-terms')
)

foreach ($p in $pages) {
    $name = $p[0]; $file = $p[1]

    # Re-resolve both the window element and the HWND for every page: the shell replaces its
    # content host when navigating, which invalidates the cached AutomationElement.
    $hwnd = (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue).MainWindowHandle
    if ($hwnd -eq [IntPtr]::Zero) {
        Start-Sleep -Milliseconds 800
        $hwnd = (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue).MainWindowHandle
        if ($hwnd -eq [IntPtr]::Zero) { "app gone before $file"; break }
    }

    $win = Get-AppWindow -Id $ProcessId
    if (-not $win) {
        # A transient UIA failure must not end the run: skip this page and keep going.
        "window unresolved for $file, skipping"
        continue
    }

    if (Invoke-Nav -Root $win -Label $name) {
        Start-Sleep -Milliseconds 1300
        $saved = Save-Shot -Handle $hwnd -Path (Join-Path $OutDir "$file.png")
        if ($saved) { "captured $file ($name)" } else { "print failed: $file" }
    }
    else {
        "nav missing: $name"
    }
}

'done'
