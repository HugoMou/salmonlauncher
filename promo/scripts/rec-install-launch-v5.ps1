# rec-install-launch-v5.ps1 — 重录安装（含真实下载进度）与启动（含游戏窗口出现）
param([string]$Which = 'install')

$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$OutDir = 'D:\SalmonLauncher\promo\public\clips-v5'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Set-SidebarScroll([double]$Pct) {
  $h = Get-SLWindow
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  $els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($e in $els) {
    try {
      $r = $e.Current.BoundingRectangle
      $t = $e.Current.ControlType.ProgrammaticName -replace 'ControlType\.', ''
      if ($t -eq 'Pane' -and [int]$r.Width -eq 352) {
        $sp = $null
        if ($e.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$sp)) { $sp.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, $Pct); return }
      }
    } catch {}
  }
}
function Nav([string]$page, [double]$scroll = -1) {
  if ($scroll -ge 0) { Set-SidebarScroll $scroll; Start-Sleep -Milliseconds 450 }
  $ok = Click-UIName $page -Exact
  if (-not $ok) { foreach ($p in 0,25,40,50,100) { Set-SidebarScroll $p; Start-Sleep -Milliseconds 350; $ok = Click-UIName $page -Exact; if ($ok) { break } } }
  Start-Sleep -Milliseconds 1300
  return $ok
}
function ClickButton([string]$name) {
  $h = Get-SLWindow
  $cr = [W]::ClientScreenRect($h)
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  foreach ($e in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
    try {
      if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' -and $e.Current.Name -eq $name) {
        $r = $e.Current.BoundingRectangle
        if ($r.Width -le 0) { continue }
        Move-CursorSmooth -X ([int]($r.X + $r.Width/2) - $cr[0]) -Y ([int]($r.Y + $r.Height/2) - $cr[1])
        Start-Sleep -Milliseconds 160
        $inv = $null
        if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$inv)) { try { $inv.Invoke(); return $true } catch {} }
        [W]::Click([int]($r.X + $r.Width/2), [int]($r.Y + $r.Height/2)); return $true
      }
    } catch {}
  }
  return $false
}

function Start-Rec([string]$file, [int]$sec) {
  $job = Start-Job -ScriptBlock {
    param($o, $t)
    & ffmpeg -y -v error -f lavfi -i "ddagrab=0:framerate=30" -t $t -vf "hwdownload,format=bgra" -c:v libx264 -preset veryfast -crf 16 -pix_fmt yuv420p $o 2>&1
  } -ArgumentList $file, $sec
  return $job
}

[W]::ShowWindow((Get-SLWindow), 3) | Out-Null
Start-Sleep -Seconds 2
[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
Start-Sleep -Milliseconds 700

if ($Which -eq 'install') {
  $file = Join-Path $OutDir '02-install.mp4'
  if (Test-Path $file) { Remove-Item $file -Force }
  $secs = 70
  $job = Start-Rec $file $secs
  $sw = [System.Diagnostics.Stopwatch]::StartNew()
  Start-Sleep -Milliseconds 900
  "--- 02-install ---"
  Nav '游戏版本' -1 | Out-Null; "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] nav-versions"; Start-Sleep -Milliseconds 1800
  Click-UIName '1.21.11' -Exact | Out-Null; "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] pick-version"; Start-Sleep -Milliseconds 1200
  Click-UIName '下一步' -Exact | Out-Null; "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] to-loaders"; Start-Sleep -Milliseconds 1600
  ClickButton '选择' | Out-Null; "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] pick-vanilla"; Start-Sleep -Milliseconds 1800
  ClickButton '开始安装' | Out-Null; "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] start-install"
  for ($i=0; $i -lt 40; $i++) {
    Start-Sleep -Seconds 1
    $inst = "$env:APPDATA\SalmonLauncher\instances\1.21.11"
    if (Test-Path "$inst\versions") { "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] instance-created"; break }
  }
  Start-Sleep -Seconds 6
  $job | Wait-Job -Timeout ($secs + 60) | Out-Null
  $job | Receive-Job | Select-Object -Last 2
  Remove-Job $job -Force -ErrorAction SilentlyContinue
  if (Test-Path $file) { "OK $([math]::Round((Get-Item $file).Length/1MB,2)) MB" } else { 'FAILED' }
}

if ($Which -eq 'launch') {
  $file = Join-Path $OutDir '03-launch.mp4'
  if (Test-Path $file) { Remove-Item $file -Force }
  $secs = 85
  $job = Start-Rec $file $secs
  $sw = [System.Diagnostics.Stopwatch]::StartNew()
  Start-Sleep -Milliseconds 900
  "--- 03-launch ---"
  Nav '启动面板' 0 | Out-Null; "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] nav-launch"
  Start-Sleep -Milliseconds 1200
  ClickButton '启动 Minecraft' | Out-Null
  if (-not $?) { }
  "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] click-start"
  # 等游戏窗口出现
  $seen = $false
  for ($i=0; $i -lt 50; $i++) {
    Start-Sleep -Seconds 1
    $g = Get-Process -Name 'javaw','java' -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if ($g) { "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] game-window pid=$($g.Id)"; $seen = $true; break }
  }
  if (-not $seen) { "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] no-game-window" }
  Start-Sleep -Seconds 8
  $job | Wait-Job -Timeout ($secs + 60) | Out-Null
  $job | Receive-Job | Select-Object -Last 2
  Remove-Job $job -Force -ErrorAction SilentlyContinue
  if (Test-Path $file) { "OK $([math]::Round((Get-Item $file).Length/1MB,2)) MB" } else { 'FAILED' }
}

Get-ChildItem $OutDir -Filter '*.mp4' | Select-Object Name, @{n='MB';e={[math]::Round($_.Length/1MB,2)}} | Format-Table -AutoSize