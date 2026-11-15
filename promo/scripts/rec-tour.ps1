# rec-tour.ps1 — 录制功能页面巡览
# 侧边栏为可滚动列表：上半部分直接可见，其余需滚动到底部后点击
param([int]$Seconds = 130)
$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$out = 'D:\SalmonLauncher\promo\public\clips\05-tour.mp4'
$w0 = Set-SLWindow -X 0 -Y 0 -W 1770 -H 984
[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
Start-Sleep -Milliseconds 800
$cr = Get-SLClientRect
"client: $($cr.X),$($cr.Y) $($cr.W)x$($cr.H)"

$marks = New-Object System.Collections.ArrayList
$sw = [System.Diagnostics.Stopwatch]::StartNew()
function Mark([string]$Name) {
    [void]$script:marks.Add([pscustomobject]@{T=[math]::Round($script:sw.Elapsed.TotalSeconds,2); Event=$Name})
    "[$([math]::Round($sw.Elapsed.TotalSeconds,2))s] $Name"
}

# 侧边栏滚动控制
function Set-SidebarScroll([double]$Percent) {
    $h = Get-SLWindow
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
    $els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $els) {
        try {
            $r = $e.Current.BoundingRectangle
            $t = $e.Current.ControlType.ProgrammaticName -replace 'ControlType\.',''
            if ($t -eq 'Pane' -and [int]$r.Width -eq 352) {
                $sp = $null
                if ($e.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$sp)) {
                    $sp.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, $Percent)
                    return $true
                }
            }
        } catch {}
    }
    return $false
}

function Nav-And-Record([string]$Page) {
    # 先在当前可见区域点；失败则滚动到底再点
    $ok = Click-UIName $Page -Exact
    if (-not $ok) {
        Set-SidebarScroll 100 | Out-Null
        Start-Sleep -Milliseconds 600
        $ok = Click-UIName $Page -Exact
    }
    return $ok
}

[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
Set-SidebarScroll 0 | Out-Null
Start-Sleep -Milliseconds 700
Click-UIName '启动面板' | Out-Null
Start-Sleep -Milliseconds 1000

$rec = Start-SLRecord -OutFile $out -Seconds $Seconds -Fps 30
$sw.Restart()
"recording -> $out"
Start-Sleep -Milliseconds 900

# 上半部分（无需滚动）
$top = @('任务中心','概览','模组管理','存档','实例设置','实例日志','游戏版本')
foreach ($p in $top) {
    $ok = Nav-And-Record $p
    Mark "page:$p=$ok"
    Start-Sleep -Milliseconds 2000
}

# 滚动到底部，录到滚动动作本身
Mark 'scroll-sidebar'
foreach ($p in @('加载器选择','安装概览','模组仓库','光影仓库','资源包仓库','资源包','光影')) {
    $ok = Nav-And-Record $p
    Mark "page:$p=$ok"
    Start-Sleep -Milliseconds 1900
}

foreach ($p in @('联机','全局偏好','崩溃诊断','应用日志','外观与设置','皮肤站','关于','用户协议')) {
    $ok = Nav-And-Record $p
    Mark "page:$p=$ok"
    Start-Sleep -Milliseconds 1900
}

Wait-SLRecord $rec
$marks | Format-Table -AutoSize
if (Test-Path $out) { "OK size=$([math]::Round((Get-Item $out).Length/1MB,2))MB" } else { 'RECORD FAILED' }