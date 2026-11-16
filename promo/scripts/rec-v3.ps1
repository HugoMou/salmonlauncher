# rec-v3.ps1 — 批量录制「动态演示」素材（启动器侧操作）
# 每段：置顶前台 → 录制 → 抽帧 OCR 校验 → 不合格自动重录（最多 3 次）
param([string]$Only = '')

$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$outDir = 'D:\SalmonLauncher\promo\public\clips-v3'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$tmpDir = Join-Path $outDir '_t'
New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null

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
        if ($e.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$sp)) {
          $sp.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, $Pct); return
        }
      }
    } catch {}
  }
}

function Nav([string]$page, [double]$scroll = -1) {
  if ($scroll -ge 0) { Set-SidebarScroll $scroll; Start-Sleep -Milliseconds 500 }
  $ok = Click-UIName $page -Exact
  if (-not $ok) {
    foreach ($pct in 0, 25, 40, 50, 100) {
      Set-SidebarScroll $pct; Start-Sleep -Milliseconds 400
      $ok = Click-UIName $page -Exact
      if ($ok) { break }
    }
  }
  Start-Sleep -Milliseconds 1400
  return $ok
}

function Click-FirstButton([string]$name) {
  $h = Get-SLWindow
  $cr = [W]::ClientScreenRect($h)
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  $els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($e in $els) {
    try {
      if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' -and $e.Current.Name -eq $name) {
        $r = $e.Current.BoundingRectangle
        if ($r.Width -le 0) { continue }
        Move-CursorSmooth -X ([int]($r.X + $r.Width / 2) - $cr[0]) -Y ([int]($r.Y + $r.Height / 2) - $cr[1])
        Start-Sleep -Milliseconds 180
        $inv = $null
        if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$inv)) { try { $inv.Invoke(); return $true } catch {} }
        [W]::Click([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
        return $true
      }
    } catch {}
  }
  return $false
}

function Set-FirstSearchValue([string]$value) {
  $h = Get-SLWindow
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  $els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($e in $els) {
    try {
      if ($e.Current.ControlType.ProgrammaticName -ne 'ControlType.Edit') { continue }
      $r = $e.Current.BoundingRectangle
      if ($r.Y -gt 400) { continue }
      $vp = $null
      if ($e.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$vp)) { $vp.SetValue($value); return $true }
    } catch {}
  }
  return $false
}

# 校验：抽 2 帧 OCR，必须命中期望关键字
function Test-Clip([string]$file, [string]$expect) {
  $probe = Join-Path $tmpDir ("probe-" + [System.IO.Path]::GetFileNameWithoutExtension($file) + ".png")
  ffmpeg -y -v error -ss 4 -i $file -frames:v 1 $probe 2>&1 | Out-Null
  $txt = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\ocr5.ps1" -Path $probe 2>&1 | Select-Object -Skip 1
  $hit = ($txt -join ' ') -replace ' ', ''
  return ($hit -like "*$expect*")
}

function Record([string]$name, [int]$seconds, [string]$expect, [scriptblock]$actions) {
  if ($Only -ne '' -and $name -ne $Only) { return }
  $file = Join-Path $outDir "$name.mp4"
  for ($attempt = 1; $attempt -le 3; $attempt++) {
    if (Test-Path $file) { Remove-Item $file -Force }
    Set-SLTopMost -On:$true
    [W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
    Start-Sleep -Milliseconds 600
    $marks = New-Object System.Collections.ArrayList
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $rec = Start-SLRecord -OutFile $file -Seconds $seconds -Fps 30
    "--- $name 第 $attempt 次录制（$seconds s）---"
    Start-Sleep -Milliseconds 500
    & $actions $marks $sw
    Start-Sleep -Milliseconds 900
    Wait-SLRecord $rec | Out-Null
    Set-SLTopMost -On:$false
    $marks | ForEach-Object { "  [$($_.T)s] $($_.Event)" }
    if (-not (Test-Path $file)) { "  录制失败，重试"; continue }
    $ok = Test-Clip $file $expect
    if ($ok) {
      "  OK $([math]::Round((Get-Item $file).Length / 1MB, 2)) MB（校验通过：$expect）"
      return
    }
    "  校验未命中「$expect」，重录"
  }
}

[W]::Move((Get-SLWindow), 0, 0, 1770, 984)
Start-Sleep -Seconds 2

# ---------- 09 光影安装 ----------
Record '09-shader-install' 32 '光影仓库' {
  param($marks, $sw)
  Nav '光影仓库' 40 | Out-Null
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='nav-shaders' })
  Start-Sleep -Milliseconds 2200
  Set-FirstSearchValue 'complementary' | Out-Null
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='type-search' })
  Start-Sleep -Milliseconds 900
  Click-UIName '搜索' -Exact | Out-Null
  Start-Sleep -Milliseconds 2400
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='search' })
  Click-FirstButton '下载并安装' | Out-Null
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='install-click' })
  for ($i = 0; $i -lt 14; $i++) { Start-Sleep -Milliseconds 900 }
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='wait-download' })
}

# ---------- 12 资源包 ----------
Record '12-packs-switch' 32 '资源包仓库' {
  param($marks, $sw)
  Nav '资源包仓库' 40 | Out-Null
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='nav-packs' })
  Start-Sleep -Milliseconds 2400
  Click-FirstButton '下载并安装' | Out-Null
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='install-pack' })
  for ($i = 0; $i -lt 16; $i++) { Start-Sleep -Milliseconds 900 }
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='wait-pack' })
}

# ---------- 16 存档 ----------
Record '16-saves-backup' 26 '存档' {
  param($marks, $sw)
  Nav '存档' 0 | Out-Null
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='nav-saves' })
  Start-Sleep -Milliseconds 2600
  Get-UIElements | Where-Object { $_.Name -and $_.X -gt 380 } | Select-Object -First 12 Name, Type, X, Y | Format-Table -AutoSize | Out-String | Write-Host
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='inspect' })
  Start-Sleep -Milliseconds 9000
}

# ---------- 17 日志 ----------
Record '17-logs-scroll' 24 '实例日志' {
  param($marks, $sw)
  Nav '实例日志' 0 | Out-Null
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='nav-logs' })
  Start-Sleep -Milliseconds 2200
  Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public class WH2 {
 [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
 public static void Scroll(int x, int y, int notches) {
   SetCursorPos(x, y); System.Threading.Thread.Sleep(150);
   for (int i=0;i<Math.Abs(notches);i++){ mouse_event(0x0800,0,0,(uint)(notches>0?120:65416),IntPtr.Zero); System.Threading.Thread.Sleep(110); }
 }
}
"@ -ErrorAction SilentlyContinue
  [WH2]::Scroll(1050, 700, -6)
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='scroll-down' })
  Start-Sleep -Milliseconds 900
  [WH2]::Scroll(1050, 700, 4)
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='scroll-up' })
  Start-Sleep -Milliseconds 3000
}

# ---------- 19 实例概览 ----------
Record '19-instance-toggles' 26 '概览' {
  param($marks, $sw)
  Nav '概览' 0 | Out-Null
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='nav-overview' })
  Start-Sleep -Milliseconds 2600
  Get-UIElements | Where-Object { $_.Name -and $_.X -gt 380 } | Select-Object -First 12 Name, Type, X, Y | Format-Table -AutoSize | Out-String | Write-Host
  [void]$marks.Add([pscustomobject]@{T=[math]::Round($sw.Elapsed.TotalSeconds, 2); Event='inspect' })
  Start-Sleep -Milliseconds 9000
}

"=== 结束 ==="
Get-ChildItem $outDir -Filter '*.mp4' | Select-Object Name, @{n='MB'; e={[math]::Round($_.Length / 1MB, 2)}} | Format-Table -AutoSize
