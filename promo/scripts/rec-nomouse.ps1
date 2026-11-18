# rec-nomouse.ps1 — 重录 install / launch，全程不移动鼠标（只用 UI Automation 触发）
param([string]$Which = 'install')

$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$OutDir = 'D:\SalmonLauncher\promo\public\clips-v5'
$Tmp = Join-Path $OutDir '_t'
New-Item -ItemType Directory -Force -Path $OutDir, $Tmp | Out-Null

# 纯 UIA：按名称触发，不移动光标
function Invoke-ByName([string]$name) {
  $h = Get-SLWindow
  if ($h -eq [IntPtr]::Zero) { return $false }
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  $els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  $cands = @()
  $interactive = @('ListItem','Button','Hyperlink','RadioButton','CheckBox','ToggleSwitch','ComboBox','MenuItem','TabItem')
  foreach ($e in $els) {
    try {
      $n = $e.Current.Name
      if ([string]::IsNullOrWhiteSpace($n) -or $n -ne $name) { continue }
      $r = $e.Current.BoundingRectangle
      if ($r.Width -le 0) { continue }
      $t = $e.Current.ControlType.ProgrammaticName -replace 'ControlType\.',''
      $score = if ($interactive -contains $t) { 50 } else { 0 }
      $cands += [pscustomobject]@{El=$e; Score=$score; Type=$t}
    } catch {}
  }
  foreach ($c in ($cands | Sort-Object Score -Descending)) {
    try {
      $pat = $null
      if ($c.El.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pat)) { $pat.Invoke(); return $true }
      $sel = $null
      if ($c.El.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$sel)) { $sel.Select(); return $true }
    } catch {}
  }
  return $false
}

function Set-SidebarScroll([double]$Pct) {
  $h = Get-SLWindow
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  foreach ($e in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
    try {
      $r = $e.Current.BoundingRectangle
      $t = $e.Current.ControlType.ProgrammaticName -replace 'ControlType\.',''
      if ($t -eq 'Pane' -and [int]$r.Width -eq 352) {
        $sp = $null
        if ($e.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$sp)) { $sp.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, $Pct); return }
      }
    } catch {}
  }
}
function Nav([string]$page, [double]$scroll = -1) {
  if ($scroll -ge 0) { Set-SidebarScroll $scroll; Start-Sleep -Milliseconds 400 }
  $ok = Invoke-ByName $page
  if (-not $ok) { foreach ($p in 0,25,40,50,100) { Set-SidebarScroll $p; Start-Sleep -Milliseconds 300; $ok = Invoke-ByName $page; if ($ok) { break } } }
  Start-Sleep -Milliseconds 1300
  return $ok
}

function Start-Rec([string]$file, [int]$sec) {
  Start-Job -ScriptBlock {
    param($o, $t)
    & ffmpeg -y -v error -f lavfi -i "ddagrab=0:framerate=30" -t $t -vf "hwdownload,format=bgra" -c:v libx264 -preset veryfast -crf 16 -pix_fmt yuv420p $o 2>&1
  } -ArgumentList $file, $sec
}

function Test-Clean([string]$file) {
  $probe = Join-Path $Tmp ("chk-" + [System.IO.Path]::GetFileNameWithoutExtension($file) + ".png")
  foreach ($t in 5, 12, 20) {
    ffmpeg -y -v error -ss $t -i $file -frames:v 1 $probe 2>&1 | Out-Null
    if (-not (Test-Path $probe)) { continue }
    $txt = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\ocr5.ps1" -Path $probe 2>&1 | Select-Object -Skip 1) -join ' '
    if ($txt -match 'SalmonLauncher|启动面板|任务中心|实例|模组|光影|资源包|安装概览|游戏版本|主题工坊|皮肤|联机|存档|日志|偏好|设置') { return $true }
  }
  return $false
}

[W]::ShowWindow((Get-SLWindow), 3) | Out-Null
Start-Sleep -Seconds 2
Set-SLTopMost -On:$true
Start-Sleep -Milliseconds 500

if ($Which -eq 'install') {
  $file = Join-Path $OutDir '02-install.mp4'
  for ($attempt = 1; $attempt -le 3; $attempt++) {
    if (Test-Path $file) { Remove-Item $file -Force }
    $dl = [math]::Round((Get-ChildItem 'D:\SalmonLauncher\promo\public\clips-v5\_t' -File -ErrorAction SilentlyContinue | Measure-Object).Count)
    $secs = 75
    $job = Start-Rec $file $secs
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Milliseconds 900
    "--- 02-install 第 $attempt 次 ---"
    Nav '游戏版本' 0 | Out-Null; "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] nav"
    Start-Sleep -Milliseconds 1600
    Invoke-ByName '1.21.11' | Out-Null; "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] pick-version"
    Start-Sleep -Milliseconds 1200
    Invoke-ByName '下一步' | Out-Null; "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] next"
    Start-Sleep -Milliseconds 1500
    # 原版（第一个「选择」按钮）
    $h = Get-SLWindow
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
    foreach ($e in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
      try {
        if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' -and $e.Current.Name -eq '选择') {
          $inv = $null
          if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$inv)) { $inv.Invoke(); break }
        }
      } catch {}
    }
    "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] pick-vanilla"; Start-Sleep -Milliseconds 1700
    Invoke-ByName '开始安装' | Out-Null; "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] start-install"
    for ($i = 0; $i -lt 55; $i++) {
      Start-Sleep -Seconds 1
      if (Test-Path "$env:APPDATA\SalmonLauncher\instances\1.21.11\versions") { "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] instance-created"; break }
    }
    Start-Sleep -Seconds 5
    $job | Wait-Job -Timeout ($secs + 90) | Out-Null
    $job | Receive-Job | Select-Object -Last 2
    Remove-Job $job -Force -ErrorAction SilentlyContinue
    if ((Test-Path $file) -and (Test-Clean $file)) {
      "  OK $([math]::Round((Get-Item $file).Length/1MB,2)) MB（校验通过）"; break
    }
    "  校验失败，重录"
  }
}

if ($Which -eq 'launch') {
  $file = Join-Path $OutDir '03-launch.mp4'
  for ($attempt = 1; $attempt -le 3; $attempt++) {
    if (Test-Path $file) { Remove-Item $file -Force }
    $secs = 95
    $job = Start-Rec $file $secs
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Milliseconds 900
    "--- 03-launch 第 $attempt 次 ---"
    Nav '启动面板' 0 | Out-Null; "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] nav"
    Start-Sleep -Milliseconds 1200
    Invoke-ByName '启动 Minecraft' | Out-Null; "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] click-start"
    for ($i = 0; $i -lt 60; $i++) {
      Start-Sleep -Seconds 1
      $g = Get-Process -Name 'javaw','java' -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
      if ($g) { "[$([math]::Round($sw.Elapsed.TotalSeconds,1))s] game-window"; break }
    }
    Start-Sleep -Seconds 10
    $job | Wait-Job -Timeout ($secs + 90) | Out-Null
    $job | Receive-Job | Select-Object -Last 2
    Remove-Job $job -Force -ErrorAction SilentlyContinue
    if ((Test-Path $file) -and (Test-Clean $file)) {
      "  OK $([math]::Round((Get-Item $file).Length/1MB,2)) MB（校验通过）"; break
    }
    "  校验失败，重录"
  }
}

Set-SLTopMost -On:$false
Get-ChildItem $OutDir -Filter '0*.mp4' | Select-Object Name,@{n='MB';e={[math]::Round($_.Length/1MB,2)}} | Format-Table -AutoSize