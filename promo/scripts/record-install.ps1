# record-install.ps1 — 录制「游戏版本 → 加载器 → 安装 → 下载进度」
param([int]$Seconds = 150, [string]$Version = '1.21.11')
$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$out = 'D:\SalmonLauncher\promo\public\clips\02-install.mp4'
$w = Set-SLWindow -X 0 -Y 0 -W 1770 -H 1106
"client: $($w.X),$($w.Y) $($w.W)x$($w.H)"

$marks = @()
function Mark([string]$Name) {
    $script:marks += [pscustomobject]@{T=[math]::Round($script:sw.Elapsed.TotalSeconds,2); Event=$Name}
    "[$([math]::Round($script:sw.Elapsed.TotalSeconds,2))s] $Name"
}

Start-Sleep -Milliseconds 800
$rec = Start-SLRecord -OutFile $out -Seconds $Seconds -Fps 30
$sw = [System.Diagnostics.Stopwatch]::StartNew()
"recording -> $out"
Start-Sleep -Milliseconds 1200

# 进入游戏版本页
Click-UIName '游戏版本' -Mouse | Out-Null
Mark 'nav-versions'
Start-Sleep -Milliseconds 2500

# 选择版本
Click-UIName $Version -Exact -Mouse | Out-Null
Mark "pick-$Version"
Start-Sleep -Milliseconds 1500

Click-UIName '下一步' -Exact -Mouse | Out-Null
Mark 'to-loaders'
Start-Sleep -Milliseconds 2200

# 选择原版（第一个「选择」按钮）
$h = Get-SLWindow
$root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
$els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$cr = [W]::ClientScreenRect($h)
foreach ($e in $els) {
    try {
        if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' -and $e.Current.Name -eq '选择') {
            $r = $e.Current.BoundingRectangle
            $cx = [int]($r.X + $r.Width/2); $cy = [int]($r.Y + $r.Height/2)
            Move-CursorSmooth -X ($cx - $cr[0]) -Y ($cy - $cr[1])
            Start-Sleep -Milliseconds 120
            [W]::Click($cx, $cy)
            break
        }
    } catch {}
}
Mark 'pick-vanilla'
Start-Sleep -Milliseconds 2500

Click-UIName '开始安装' -Mouse | Out-Null
Mark 'start-install'
Start-Sleep -Milliseconds 1500

# 轮询安装完成（最多 110 秒）
$done = $false
for ($i = 0; $i -lt 110; $i++) {
    Start-Sleep -Seconds 1
    $els = Get-UIElements
    $hit = $els | Where-Object { $_.Name -match '安装完成|已安装|启动实例|完成安装' } | Select-Object -First 1
    if ($hit) { Mark ('install-done:' + $hit.Name); $done = $true; break }
}
if (-not $done) { Mark 'install-timeout' }
Start-Sleep -Milliseconds 2500

Wait-SLRecord $rec
$marks | Format-Table -AutoSize
if (Test-Path $out) {
    $f = Get-Item $out
    "OK size=$([math]::Round($f.Length/1MB,2))MB"
} else { 'RECORD FAILED' }