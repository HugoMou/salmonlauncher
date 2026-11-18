# re-record-specific.ps1 — 重录失败的几个片段
$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$OutDir = 'D:\SalmonLauncher\promo\public\clips-v5'
$Tmp = Join-Path $OutDir '_t'
New-Item -ItemType Directory -Force -Path $OutDir, $Tmp | Out-Null

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
  Start-Sleep -Milliseconds 1500
  return $ok
}

function Start-Rec([string]$file, [int]$sec) {
  Start-Job -ScriptBlock {
    param($o, $t)
    & ffmpeg -y -v error -f gdigrab -framerate 30 -i desktop -t $t -vf "format=bgra" -c:v libx264 -preset veryfast -crf 16 -pix_fmt yuv420p $o 2>&1
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

function Ensure-SLReady {
  $h = Get-SLWindow
  [W]::SetWindowPos($h, [IntPtr](-1), 0, 0, 1936, 1152, 0x0040) | Out-Null
  Start-Sleep -Milliseconds 300
  [W]::SetWindowPos($h, [IntPtr](-1), 0, 0, 1936, 1152, 0x0040) | Out-Null
  Start-Sleep -Milliseconds 300
  [W]::ShowWindow($h, 3) | Out-Null
  Start-Sleep -Milliseconds 500
  Set-SLTopMost -On:$true
  Start-Sleep -Milliseconds 300
  [W]::SetForegroundWindow($h) | Out-Null
  Start-Sleep -Milliseconds 800
}

# 12-saves
$file = Join-Path $OutDir '12-saves.mp4'
for ($attempt = 1; $attempt -le 3; $attempt++) {
  if (Test-Path $file) { Remove-Item $file -Force }
  Ensure-SLReady
  $secs = 22
  $job = Start-Rec $file $secs
  Start-Sleep -Milliseconds 900
  Nav '存档' 0 | Out-Null; Start-Sleep -Milliseconds 3000
  # 多等一会儿让页面稳定
  Start-Sleep -Milliseconds 5000
  $job | Wait-Job -Timeout ($secs + 90) | Out-Null
  $job | Receive-Job | Select-Object -Last 2
  Remove-Job $job -Force -ErrorAction SilentlyContinue
  Set-SLTopMost -On:$false
  if ((Test-Path $file) -and (Test-Clean $file)) { "12-saves OK $([math]::Round((Get-Item $file).Length/1MB,2)) MB"; break }
  "12-saves 校验失败，重录"
}

# 13-logs
$file = Join-Path $OutDir '13-logs.mp4'
for ($attempt = 1; $attempt -le 3; $attempt++) {
  if (Test-Path $file) { Remove-Item $file -Force }
  Ensure-SLReady
  $secs = 22
  $job = Start-Rec $file $secs
  Start-Sleep -Milliseconds 900
  Nav '实例日志' 0 | Out-Null; Start-Sleep -Milliseconds 3000
  Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public class WS5 {
 [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
 public static void Scroll(int x,int y,int n){ SetCursorPos(x,y); System.Threading.Thread.Sleep(150);
  for(int i=0;i<Math.Abs(n);i++){ mouse_event(0x0800,0,0,(uint)(n>0?120:65416),IntPtr.Zero); System.Threading.Thread.Sleep(110);} }
}
"@ -ErrorAction SilentlyContinue
  $cr = Get-SLClientRect
  [WS5]::Scroll(1100, 700, -6); Start-Sleep -Milliseconds 800
  [WS5]::Scroll(1100, 700, 3); Start-Sleep -Milliseconds 3000
  $job | Wait-Job -Timeout ($secs + 90) | Out-Null
  $job | Receive-Job | Select-Object -Last 2
  Remove-Job $job -Force -ErrorAction SilentlyContinue
  Set-SLTopMost -On:$false
  if ((Test-Path $file) -and (Test-Clean $file)) { "13-logs OK $([math]::Round((Get-Item $file).Length/1MB,2)) MB"; break }
  "13-logs 校验失败，重录"
}

# 14-overview
$file = Join-Path $OutDir '14-overview.mp4'
for ($attempt = 1; $attempt -le 3; $attempt++) {
  if (Test-Path $file) { Remove-Item $file -Force }
  Ensure-SLReady
  $secs = 22
  $job = Start-Rec $file $secs
  Start-Sleep -Milliseconds 900
  Nav '概览' 0 | Out-Null; Start-Sleep -Milliseconds 3000
  Start-Sleep -Milliseconds 5000
  $job | Wait-Job -Timeout ($secs + 90) | Out-Null
  $job | Receive-Job | Select-Object -Last 2
  Remove-Job $job -Force -ErrorAction SilentlyContinue
  Set-SLTopMost -On:$false
  if ((Test-Path $file) -and (Test-Clean $file)) { "14-overview OK $([math]::Round((Get-Item $file).Length/1MB,2)) MB"; break }
  "14-overview 校验失败，重录"
}

# 15-mods
$file = Join-Path $OutDir '15-mods.mp4'
for ($attempt = 1; $attempt -le 3; $attempt++) {
  if (Test-Path $file) { Remove-Item $file -Force }
  Ensure-SLReady
  $secs = 30
  $job = Start-Rec $file $secs
  Start-Sleep -Milliseconds 900
  Nav '模组仓库' 40 | Out-Null; Start-Sleep -Milliseconds 3000
  $h = Get-SLWindow
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  foreach ($e in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
    try {
      if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Edit' -and $e.Current.BoundingRectangle.Y -lt 420) {
        $vp = $null
        if ($e.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$vp)) { $vp.SetValue('sodium'); break }
      }
    } catch {}
  }
  Start-Sleep -Milliseconds 800
  Invoke-ByName '搜索' | Out-Null; Start-Sleep -Milliseconds 2200
  $h = Get-SLWindow
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  foreach ($e in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
    try {
      if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' -and $e.Current.Name -eq '下载并安装') {
        $inv = $null
        if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$inv)) { $inv.Invoke(); break }
      }
    } catch {}
  }
  Start-Sleep -Milliseconds 9000
  $job | Wait-Job -Timeout ($secs + 90) | Out-Null
  $job | Receive-Job | Select-Object -Last 2
  Remove-Job $job -Force -ErrorAction SilentlyContinue
  Set-SLTopMost -On:$false
  if ((Test-Path $file) -and (Test-Clean $file)) { "15-mods OK $([math]::Round((Get-Item $file).Length/1MB,2)) MB"; break }
  "15-mods 校验失败，重录"
}

Get-ChildItem $OutDir -Filter '1*.mp4' | Select-Object Name,@{n='MB';e={[math]::Round($_.Length/1MB,2)}} | Format-Table -AutoSize