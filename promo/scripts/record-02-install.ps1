# record-02-install.ps1 — 录制「游戏版本 → 加载器 → 安装 → 下载进度」
param([int]$Seconds = 150, [string]$Version = '1.21.11')
$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$out = 'D:\SalmonLauncher\promo\public\clips\02-install.mp4'
# 固定窗口：单次 Move + 充分等待，避免反复触发布局回弹
[W]::Move((Get-SLWindow), 0, 0, 1770, 984)
Start-Sleep -Seconds 2
$cr = Get-SLClientRect
"client: $($cr.X),$($cr.Y) $($cr.W)x$($cr.H)"

$marks = New-Object System.Collections.ArrayList
$sw = [System.Diagnostics.Stopwatch]::StartNew()
function Mark([string]$Name) {
    [void]$script:marks.Add([pscustomobject]@{T=[math]::Round($script:sw.Elapsed.TotalSeconds,2); Event=$Name})
    "[$([math]::Round($sw.Elapsed.TotalSeconds,2))s] $Name"
}

Start-Sleep -Milliseconds 500
$rec = Start-SLRecord -OutFile $out -Seconds $Seconds -Fps 30
$sw.Restart()
"recording -> $out"
Start-Sleep -Milliseconds 1200

Click-UIName '游戏版本' | Out-Null
Mark 'nav-versions'
Start-Sleep -Milliseconds 2200

Click-UIName $Version -Exact | Out-Null
Mark "pick-$Version"
Start-Sleep -Milliseconds 1400

Click-UIName '下一步' -Exact | Out-Null
Mark 'to-loaders'
Start-Sleep -Milliseconds 2000

# 原版(纯净) 行的第一个「选择」
$h = Get-SLWindow
$root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
$els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$cr = [W]::ClientScreenRect($h)
foreach ($e in $els) {
    try {
        if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' -and $e.Current.Name -eq '选择') {
            $r = $e.Current.BoundingRectangle
            Move-CursorSmooth -X ([int]($r.X + $r.Width/2) - $cr[0]) -Y ([int]($r.Y + $r.Height/2) - $cr[1])
            Start-Sleep -Milliseconds 150
            $inv = $null
            if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$inv)) { $inv.Invoke() }
            else { [W]::Click([int]($r.X+$r.Width/2), [int]($r.Y+$r.Height/2)) }
            break
        }
    } catch {}
}
Mark 'pick-vanilla'
Start-Sleep -Milliseconds 2400

Click-UIName '开始安装' | Out-Null
Mark 'start-install'

# 记录进度文本，最多 120 秒
$probe = New-Object System.Collections.ArrayList
$done = $false
for ($i = 0; $i -lt 120; $i++) {
    Start-Sleep -Seconds 1
    $els = Get-UIElements
    if ($i % 4 -eq 0) {
        $txt = ($els | Where-Object { $_.Name -and $_.Type -eq 'Text' -and $_.X -gt 400 } | Select-Object -ExpandProperty Name) -join ' | '
        [void]$probe.Add(('t=' + [math]::Round($sw.Elapsed.TotalSeconds,1) + ' :: ' + $txt))
    }
    $hit = $els | Where-Object { $_.Name -match '安装完成|已安装|完成安装|可以启动' } | Select-Object -First 1
    if ($hit) { Mark ('install-done:' + $hit.Name); $done = $true; break }
}
if (-not $done) { Mark 'install-timeout' }
Start-Sleep -Milliseconds 2000

Wait-SLRecord $rec
$marks | Format-Table -AutoSize
$probe | Out-File 'D:\SalmonLauncher\promo\public\clips\02-install.probe.txt' -Encoding utf8
if (Test-Path $out) {
    $f = Get-Item $out
    "OK size=$([math]::Round($f.Length/1MB,2))MB"
} else { 'RECORD FAILED' }