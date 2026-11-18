# rec-all-v5.ps1 — 全部素材重录（ddagrab 桌面捕获 1920×1080，带打点与自动校验）
# 用法: rec-all-v5.ps1 -Only '05-tour,07-theme'   或   -Only all
param([string]$Only = 'all')

$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$OutDir = 'D:\SalmonLauncher\promo\public\clips-v5'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$Tmp = Join-Path $OutDir '_t'
New-Item -ItemType Directory -Force -Path $Tmp | Out-Null
$List = if ($Only -eq 'all') { @() } else { $Only -split ',' | ForEach-Object { $_.Trim() } }

function Want([string]$n) { return ($Only -eq 'all') -or ($List -contains $n) }

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
  if ($scroll -ge 0) { Set-SidebarScroll $scroll; Start-Sleep -Milliseconds 450 }
  $ok = Click-UIName $page -Exact
  if (-not $ok) {
    foreach ($pct in 0, 25, 40, 50, 100) {
      Set-SidebarScroll $pct; Start-Sleep -Milliseconds 350
      $ok = Click-UIName $page -Exact
      if ($ok) { break }
    }
  }
  Start-Sleep -Milliseconds 1300
  return $ok
}

function ClickButton([string]$name) {
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
        Start-Sleep -Milliseconds 150
        $inv = $null
        if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$inv)) { try { $inv.Invoke(); return $true } catch {} }
        [W]::Click([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
        return $true
      }
    } catch {}
  }
  return $false
}

function SetSearch([string]$value) {
  $h = Get-SLWindow
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  $els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($e in $els) {
    try {
      if ($e.Current.ControlType.ProgrammaticName -ne 'ControlType.Edit') { continue }
      $r = $e.Current.BoundingRectangle
      if ($r.Y -gt 420) { continue }
      $vp = $null
      if ($e.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$vp)) { $vp.SetValue($value); return $true }
    } catch {}
  }
  return $false
}

# 录制：ddagrab 全屏 1920x1080 + 打点
function Test-Clean([string]$file) {
  # 抽 2 帧 OCR，必须出现启动器相关词，否则视为被其他窗口遮挡
  $probe = Join-Path $Tmp ("chk-" + [System.IO.Path]::GetFileNameWithoutExtension($file) + ".png")
  foreach ($t in 5, 12) {
    ffmpeg -y -v error -ss $t -i $file -frames:v 1 $probe 2>&1 | Out-Null
    if (-not (Test-Path $probe)) { continue }
    $txt = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\ocr5.ps1" -Path $probe 2>&1 | Select-Object -Skip 1) -join ' '
    if ($txt -match 'SalmonLauncher|启动面板|任务中心|实例|模组|光影|资源包|安装概览|游戏版本|主题工坊|皮肤|联机|存档|日志|偏好|设置') { return $true }
  }
  return $false
}

function Record([string]$name, [int]$seconds, [scriptblock]$actions) {
  if (-not (Want $name)) { return }
  $file = Join-Path $OutDir "$name.mp4"
  for ($attempt = 1; $attempt -le 3; $attempt++) {
    if (Test-Path $file) { Remove-Item $file -Force }
    [W]::ShowWindow((Get-SLWindow), 3) | Out-Null
    Start-Sleep -Milliseconds 500
    Set-SLTopMost -On:$true
    [W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
    Start-Sleep -Milliseconds 700
  $marks = New-Object System.Collections.ArrayList
  $sw = [System.Diagnostics.Stopwatch]::StartNew()
  $job = Start-Job -ScriptBlock {
    param($o, $sec)
    & ffmpeg -y -v error -f lavfi -i "ddagrab=0:framerate=30" -t $sec -vf "hwdownload,format=bgra" `
      -c:v libx264 -preset veryfast -crf 16 -pix_fmt yuv420p $o 2>&1
  } -ArgumentList $file, $seconds
  Start-Sleep -Milliseconds 900
  "--- $name ($seconds s) ---"
  & $actions $marks $sw
    $job | Wait-Job -Timeout ($seconds + 90) | Out-Null
    $job | Receive-Job | Select-Object -Last 2
    Remove-Job $job -Force -ErrorAction SilentlyContinue
    Set-SLTopMost -On:$false
    if (-not (Test-Path $file)) { "   录制失败，重试"; continue }
    $clean = Test-Clean $file
    if ($clean) {
      $marks | ForEach-Object { "   [$($_.T)s] $($_.Event)" }
      "   OK $([math]::Round((Get-Item $file).Length / 1MB, 2)) MB（校验通过，第 $attempt 次）"
      return
    }
    "   画面被遮挡或未命中，重录（第 $attempt 次）"
  }
}

$mark = { param($m, $s, $label) [void]$m.Add([pscustomobject]@{T=[math]::Round($s.Elapsed.TotalSeconds,2); Event=$label}) }

# ---------- 05 页面巡览（上半部分） ----------
Record '05-tour' 34 {
  param($m, $s)
  Nav '启动面板' 0 | Out-Null; & $mark $m $s 'start'; Start-Sleep -Milliseconds 900
  foreach ($p in '任务中心','概览','模组管理','存档','实例设置','实例日志') {
    Nav $p 0 | Out-Null; & $mark $m $s "page:$p"; Start-Sleep -Milliseconds 1800
  }
}

# ---------- 06 页面巡览（资源与系统） ----------
Record '06-tour2' 34 {
  param($m, $s)
  Nav '游戏版本' -1 | Out-Null; & $mark $m $s 'page:游戏版本'; Start-Sleep -Milliseconds 1800
  foreach ($p in '加载器选择','安装概览','模组仓库','光影仓库','资源包仓库') {
    Nav $p 40 | Out-Null; & $mark $m $s "page:$p"; Start-Sleep -Milliseconds 1700
  }
}

# ---------- 07 主题工坊 ----------
Record '07-theme' 26 {
  param($m, $s)
  Nav '主题工坊' 100 | Out-Null; & $mark $m $s 'nav-theme'; Start-Sleep -Milliseconds 1600
  $cr = Get-SLClientRect
  foreach ($pt in @(@(600,520),@(960,520),@(1320,520))) {
    Move-CursorSmooth -X $pt[0] -Y $pt[1]; Start-Sleep -Milliseconds 650
  }
  Click-UIName '草原清晨' -Exact | Out-Null; & $mark $m $s 'apply:草原清晨'; Start-Sleep -Milliseconds 2600
}

# ---------- 08 光影仓库 ----------
Record '08-shaders' 30 {
  param($m, $s)
  Nav '光影仓库' 40 | Out-Null; & $mark $m $s 'nav'; Start-Sleep -Milliseconds 1800
  SetSearch 'complementary' | Out-Null; & $mark $m $s 'type'; Start-Sleep -Milliseconds 900
  Click-UIName '搜索' -Exact | Out-Null; & $mark $m $s 'search'; Start-Sleep -Milliseconds 2200
  ClickButton '下载并安装' | Out-Null; & $mark $m $s 'install'; Start-Sleep -Milliseconds 9000
}

# ---------- 09 资源包仓库 ----------
Record '09-packs' 28 {
  param($m, $s)
  Nav '资源包仓库' 40 | Out-Null; & $mark $m $s 'nav'; Start-Sleep -Milliseconds 1800
  SetSearch 'faithful' | Out-Null; Start-Sleep -Milliseconds 800
  Click-UIName '搜索' -Exact | Out-Null; & $mark $m $s 'search'; Start-Sleep -Milliseconds 2200
  ClickButton '下载并安装' | Out-Null; & $mark $m $s 'install'; Start-Sleep -Milliseconds 9000
}

# ---------- 10 皮肤站 ----------
Record '10-skins' 24 {
  param($m, $s)
  Nav '皮肤站' 100 | Out-Null; & $mark $m $s 'nav'; Start-Sleep -Milliseconds 2200
  $cr = Get-SLClientRect
  Move-CursorSmooth -X 900 -Y 600; Start-Sleep -Milliseconds 900
  Move-CursorSmooth -X 1200 -Y 700; & $mark $m $s 'hover-3d'; Start-Sleep -Milliseconds 6000
}

# ---------- 11 联机 ----------
Record '11-online' 26 {
  param($m, $s)
  Nav '联机' 100 | Out-Null; & $mark $m $s 'nav'; Start-Sleep -Milliseconds 2400
  Move-CursorSmooth -X 760 -Y 400; Start-Sleep -Milliseconds 900
  Move-CursorSmooth -X 1000 -Y 700; & $mark $m $s 'hover'; Start-Sleep -Milliseconds 6000
}

# ---------- 12 存档 ----------
Record '12-saves' 22 {
  param($m, $s)
  Nav '存档' 0 | Out-Null; & $mark $m $s 'nav'; Start-Sleep -Milliseconds 2400
  Move-CursorSmooth -X 900 -Y 500; & $mark $m $s 'hover'; Start-Sleep -Milliseconds 5000
}

# ---------- 13 日志 ----------
Record '13-logs' 22 {
  param($m, $s)
  Nav '实例日志' 0 | Out-Null; & $mark $m $s 'nav'; Start-Sleep -Milliseconds 2000
  Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public class WS5 {
 [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
 public static void Scroll(int x,int y,int n){ SetCursorPos(x,y); System.Threading.Thread.Sleep(150);
  for(int i=0;i<Math.Abs(n);i++){ mouse_event(0x0800,0,0,(uint)(n>0?120:65416),IntPtr.Zero); System.Threading.Thread.Sleep(110);} }
}
"@ -ErrorAction SilentlyContinue
  [WS5]::Scroll(1100, 700, -6); & $mark $m $s 'scroll'; Start-Sleep -Milliseconds 800
  [WS5]::Scroll(1100, 700, 3); Start-Sleep -Milliseconds 3000
}

# ---------- 14 实例概览 ----------
Record '14-overview' 22 {
  param($m, $s)
  Nav '概览' 0 | Out-Null; & $mark $m $s 'nav'; Start-Sleep -Milliseconds 2400
  Move-CursorSmooth -X 900 -Y 420; & $mark $m $s 'hover'; Start-Sleep -Milliseconds 5000
}

# ---------- 15 模组仓库 ----------
Record '15-mods' 30 {
  param($m, $s)
  Nav '模组仓库' 40 | Out-Null; & $mark $m $s 'nav'; Start-Sleep -Milliseconds 2000
  SetSearch 'sodium' | Out-Null; Start-Sleep -Milliseconds 800
  Click-UIName '搜索' -Exact | Out-Null; & $mark $m $s 'search'; Start-Sleep -Milliseconds 2200
  ClickButton '下载并安装' | Out-Null; & $mark $m $s 'install'; Start-Sleep -Milliseconds 9000
}

"=== 录制结果 ==="
Get-ChildItem $OutDir -Filter '*.mp4' | Select-Object Name, @{n='MB'; e={[math]::Round($_.Length / 1MB, 2)}} | Format-Table -AutoSize