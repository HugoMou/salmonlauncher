# rec-launch.ps1 — 录制「选择实例 → 点击启动 → 资源下载/校验 → 进入游戏」
param([int]$Seconds = 240)
$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$out = 'D:\SalmonLauncher\promo\public\clips\03-launch.mp4'
$cr = Get-SLClientRect
"client: $($cr.X),$($cr.Y) $($cr.W)x$($cr.H)"

$marks = New-Object System.Collections.ArrayList
$probe = New-Object System.Collections.ArrayList
$sw = [System.Diagnostics.Stopwatch]::StartNew()
function Mark([string]$Name) {
    [void]$script:marks.Add([pscustomobject]@{T=[math]::Round($script:sw.Elapsed.TotalSeconds,2); Event=$Name})
    "[$([math]::Round($sw.Elapsed.TotalSeconds,2))s] $Name"
}

# 回到启动面板
Click-UIName '启动面板' | Out-Null
Start-Sleep -Milliseconds 1500

# 若实例未选，选择 1.21.11
$els = Get-UIElements
if ($els | Where-Object { $_.Name -match '先安装或选择一个实例' }) {
    Click-UIName '1.21.11' | Out-Null
    Start-Sleep -Milliseconds 1200
}

Start-Sleep -Milliseconds 400
$rec = Start-SLRecord -OutFile $out -Seconds $Seconds -Fps 30
$sw.Restart()
"recording -> $out"
Start-Sleep -Milliseconds 1200

$clicked = Click-UIName '启动 Minecraft'
if (-not $clicked) { $clicked = Click-UIName '一键启动' }
if (-not $clicked) { $clicked = Click-UIName '启动' }
Mark "click-launch:$clicked"

$game = $false
for ($i = 0; $i -lt 70; $i++) {
    Start-Sleep -Seconds 3
    $els = Get-UIElements
    if ($i % 2 -eq 0) {
        $txt = ($els | Where-Object { $_.Name -and $_.X -gt 420 -and $_.Y -lt 700 } | Select-Object -ExpandProperty Name) -join ' | '
        [void]$probe.Add(('t=' + [math]::Round($sw.Elapsed.TotalSeconds,1) + ' :: ' + $txt))
    }
    $j = Get-Process -Name 'javaw','java' -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 -or $_.WorkingSet -gt 200MB }
    if ($j) { Mark 'java-running'; $game = $true; break }
}
if (-not $game) { Mark 'no-java-detected' }
Start-Sleep -Seconds 4
# 记录游戏窗口
Get-Process -Name 'javaw','java' -ErrorAction SilentlyContinue | ForEach-Object {
    [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds,2); Event=('java pid=' + $_.Id + ' title=' + $_.MainWindowTitle)})
}
Start-Sleep -Seconds 10

Wait-SLRecord $rec
$marks | Format-Table -AutoSize
$probe | Out-File 'D:\SalmonLauncher\promo\public\clips\03-launch.probe.txt' -Encoding utf8
if (Test-Path $out) { "OK size=$([math]::Round((Get-Item $out).Length/1MB,2))MB" } else { 'RECORD FAILED' }