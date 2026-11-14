# SalmonLauncher (SL) — promotional screenshot capture
# Copyright (C) 2026 Hugo Mou — GPL-3.0-or-later
#
# Captures the launcher window itself via PrintWindow, so whatever is on top of it
# in the desktop does not matter. Every image is the real application.

param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$OutDir
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
    $rootEl = [System.Windows.Automation.AutomationElement]::RootElement
    for ($i = 0; $i -lt 25; $i++) {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $Id)
        $w = $rootEl.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
        if ($w) { return $w }
        Start-Sleep -Milliseconds 300
    }
    return $null
}

# PrintWindow with PW_RENDERFULLCONTENT grabs the window's own content even when
# another window covers it or it is partially offscreen.
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

function Invoke-Nav {
    param($Root, [string]$Label)
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $Label)
    $items = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    for ($i = 0; $i -lt $items.Count; $i++) {
        $el = $items.Item($i)
        if ($el.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) {
            try { $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); return $true }
            catch { return $false }
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

# --- Welcome flow ---
$files = @('01-welcome-language', '02-welcome-terms', '03-welcome-migrate', '04-welcome-account', '05-welcome-theme')

$onboarding = Find-Button -Root $win -Name '下一步'
if ($onboarding) {
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

        # Step 5 (theme) finishes the wizard with "开始使用" instead of "下一步".
        $btn = Find-Button -Root $win -Name '下一步'
        if (-not $btn) { $btn = Find-Button -Root $win -Name '开始使用' }
        if (-not $btn) { "no advance button at step $step"; break }
        if (-not $btn.Current.IsEnabled) { "advance disabled at step $step"; break }

        try { $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
        catch { "invoke failed at step $step"; break }

        Start-Sleep -Milliseconds 1000
        $idx = [Math]::Min($step + 1, $files.Count - 1)
        if ($step + 1 -lt $files.Count) {
            Save-Shot -Handle $hwnd -Path (Join-Path $OutDir "$($files[$step + 1]).png") | Out-Null
            'captured ' + $files[$step + 1]
        }
    }
    Start-Sleep -Milliseconds 1500
}

# Post-onboarding agreement prompt, if any.
$dlg = Find-Button -Root $win -Name '同意并继续'
if ($dlg) {
    try { $dlg.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { }
    Start-Sleep -Milliseconds 1200
}

# --- Every main page ---
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
