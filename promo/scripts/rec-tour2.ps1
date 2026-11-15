# rec-tour2.ps1 — 补录中间区域的 7 个页面（需滚动到 25%~50%）
param([int]$Seconds = 85)
$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$out = 'D:\SalmonLauncher\promo\public\clips\06-tour2.mp4'
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

function Nav-WithScroll([string]$Page, [double]$Pct) {
    Set-SidebarScroll $Pct | Out-Null
    Start-Sleep -Milliseconds 450
    $ok = Click-UIName $Page -Exact
    if (-not $ok) {
        foreach ($alt in 25, 40, 50, 15, 60) {
            Set-SidebarScroll $alt | Out-Null
            Start-Sleep -Milliseconds 400
            $ok = Click-UIName $Page -Exact
            if ($ok) { break }
        }
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

$plan = @(
    @{p='加载器选择'; s=25},
    @{p='安装概览';   s=25},
    @{p='模组仓库';   s=25},
    @{p='光影仓库';   s=40},
    @{p='资源包仓库'; s=40},
    @{p='资源包';     s=50},
    @{p='光影';       s=50}
)
foreach ($step in $plan) {
    $ok = Nav-WithScroll $step.p $step.s
    Mark "page:$($step.p)=$ok"
    Start-Sleep -Milliseconds 2100
}

Wait-SLRecord $rec
$marks | Format-Table -AutoSize
if (Test-Path $out) { "OK size=$([math]::Round((Get-Item $out).Length/1MB,2))MB" } else { 'RECORD FAILED' }