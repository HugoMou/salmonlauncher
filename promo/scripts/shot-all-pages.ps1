# shot-all-pages.ps1 — 使用当前主题（草原倾城）重新截取所有页面图片
$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$OutDir = 'D:\SalmonLauncher\promo\public\shots-v5'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

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
Start-Sleep -Milliseconds 500

function Set-SidebarScroll([double]$Pct) {
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
  $ok = $false
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
  foreach ($e in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
    try {
      if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.ListItem' -and $e.Current.Name -eq $page) {
        $inv = $null
        if ($e.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$inv)) { $inv.Select(); $ok = $true; break }
      }
    } catch {}
  }
  if (-not $ok) { 
    foreach ($p in 0,25,40,50,100) { 
      Set-SidebarScroll $p; Start-Sleep -Milliseconds 300; 
      $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
      foreach ($e in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
        try {
          if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.ListItem' -and $e.Current.Name -eq $page) {
            $inv = $null
            if ($e.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$inv)) { $inv.Select(); $ok = $true; break }
          }
        } catch {}
      }
      if ($ok) { break }
    } 
  }
  Start-Sleep -Milliseconds 1500
  return $ok
}

$pages = @(
  @{name='page-01-home'; nav='启动面板'; scroll=0},
  @{name='page-02-tasks'; nav='任务中心'; scroll=0},
  @{name='page-03-overview'; nav='概览'; scroll=0},
  @{name='page-04-mods'; nav='模组管理'; scroll=0},
  @{name='page-05-saves'; nav='存档'; scroll=0},
  @{name='page-06-inst-cfg'; nav='实例设置'; scroll=0},
  @{name='page-07-inst-logs'; nav='实例日志'; scroll=0},
  @{name='page-08-versions'; nav='游戏版本'; scroll=-1},
  @{name='page-09-loaders'; nav='加载器选择'; scroll=40},
  @{name='page-10-install'; nav='安装概览'; scroll=40},
  @{name='page-11-repo-mods'; nav='模组仓库'; scroll=40},
  @{name='page-12-repo-shaders'; nav='光影仓库'; scroll=40},
  @{name='page-13-repo-packs'; nav='资源包仓库'; scroll=40},
  @{name='page-14-online'; nav='联机'; scroll=100},
  @{name='page-15-skins'; nav='皮肤站'; scroll=100},
  @{name='page-16-diag'; nav='崩溃诊断'; scroll=0},
  @{name='page-17-app-log'; nav='应用日志'; scroll=0},
  @{name='page-18-global'; nav='全局偏好'; scroll=0},
  @{name='page-19-launch-set'; nav='启动设置'; scroll=0},
  @{name='page-20-theme'; nav='主题工坊'; scroll=100},
  @{name='page-21-about'; nav='关于'; scroll=0},
  @{name='page-22-terms'; nav='用户协议'; scroll=0}
)

foreach ($p in $pages) {
  $file = Join-Path $OutDir "$($p.name).png"
  Nav $p.nav $p.scroll | Out-Null
  Start-Sleep -Milliseconds 800
  # 使用启动器内置渲染树截图（无 DPI/遮挡问题）
  $exe = 'D:\SalmonLauncher\src\SL.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\SL.exe'
  & $exe --capture "$OutDir" --capture-page $p.name 2>&1 | Out-Null
  # 查找实际生成的文件（可能带前缀）
  $actual = Get-ChildItem $OutDir -Filter "*$($p.name)*" -File | Select-Object -Last 1
  if ($actual -and $actual.FullName -ne $file) { Move-Item $actual.FullName $file -Force }
  if (Test-Path $file) { "$($p.name) OK" } else { "$($p.name) FAILED" }
}

Set-SLTopMost -On:$false
Get-ChildItem $OutDir -Filter '*.png' | Select-Object Name, @{n='KB';e={[math]::Round($_.Length/1KB)}} | Format-Table -AutoSize