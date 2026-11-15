# rec-install.ps1 — 录制安装与下载进度（干净状态：无引导遮罩）
param([int]$Seconds = 110, [string]$Version = '1.21.11')
$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$out = 'D:\SalmonLauncher\promo\public\clips\02-install.mp4'
$cr = Get-SLClientRect
"client: $($cr.X),$($cr.Y) $($cr.W)x$($cr.H)"
if ($cr.H -gt 1010) { "WARN: 窗口过高，录屏会裁切底部" }

$marks = New-Object System.Collections.ArrayList
$probe = New-Object System.Collections.ArrayList
$sw = [System.Diagnostics.Stopwatch]::StartNew()
function Mark([string]$Name) {
    [void]$script:marks.Add([pscustomobject]@{T=[math]::Round($script:sw.Elapsed.TotalSeconds,2); Event=$Name})
    "[$([math]::Round($sw.Elapsed.TotalSeconds,2))s] $Name"
}

Start-Sleep -Milliseconds 400
$rec = Start-SLRecord -OutFile $out -Seconds $Seconds -Fps 30
$sw.Restart()
"recording -> $out ($($rec.Rect.X),$($rec.Rect.Y) $($rec.Rect.W)x$($rec.Rect.H))"
Start-Sleep -Milliseconds 1000

Click-UIName '游戏版本' | Out-Null
Mark 'nav-versions'
Start-Sleep -Milliseconds 1800

Click-UIName $Version -Exact | Out-Null
Mark "pick-$Version"
Start-Sleep -Milliseconds 1200

Click-UIName '下一步' -Exact | Out-Null
Mark 'to-loaders'
Start-Sleep -Milliseconds 1600

# 第一个「选择」按钮 = 原版
$h = Get-SLWindow
$root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
$els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$cr = [W]::ClientScreenRect($h)
foreach ($e in $els) {
    try {
        if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' -and $e.Current.Name -eq '选择') {
            $r = $e.Current.BoundingRectangle
            Move-CursorSmooth -X ([int]($r.X + $r.Width/2) - $cr[0]) -Y ([int]($r.Y + $r.Height/2) - $cr[1])
            Start-Sleep -Milliseconds 160
            $inv = $null
            if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$inv)) { $inv.Invoke() }
            else { [W]::Click([int]($r.X+$r.Width/2), [int]($r.Y+$r.Height/2)) }
            break
        }
    } catch {}
}
Mark 'pick-vanilla'
Start-Sleep -Milliseconds 1800

Click-UIName '开始安装' | Out-Null
Mark 'start-install'

$instDir = "$env:APPDATA\SalmonLauncher\instances\$Version"
$done = $false
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 2
    $els = Get-UIElements
    if ($i % 2 -eq 0) {
        $txt = ($els | Where-Object { $_.Name -and $_.X -gt 420 -and $_.Y -lt 700 } | Select-Object -ExpandProperty Name) -join ' | '
        [void]$probe.Add(('t=' + [math]::Round($sw.Elapsed.TotalSeconds,1) + ' :: ' + $txt))
    }
    if (Test-Path $instDir) { Mark 'instance-created'; $done = $true; break }
    $hit = $els | Where-Object { $_.Name -match '全部完成|已完成全部|安装成功' } | Select-Object -First 1
    if ($hit) { Mark ('done:' + $hit.Name); $done = $true; break }
}
if (-not $done) { Mark 'timeout' }
Start-Sleep -Milliseconds 2200

Wait-SLRecord $rec
$marks | Format-Table -AutoSize
$probe | Out-File 'D:\SalmonLauncher\promo\public\clips\02-install.probe.txt' -Encoding utf8
if (Test-Path $out) { "OK size=$([math]::Round((Get-Item $out).Length/1MB,2))MB" } else { 'RECORD FAILED' }