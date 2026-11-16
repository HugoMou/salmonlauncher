# rec-theme.ps1 — 补录主题工坊操作（打开 → 浏览场景 → 点击应用整套主题）
param([int]$Seconds = 26)
$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$out = 'D:\SalmonLauncher\promo\public\clips\07-theme.mp4'
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
                    $sp.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, $Percent); return $true
                }
            }
        } catch {}
    }
    return $false
}

[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
Set-SidebarScroll 0 | Out-Null
Start-Sleep -Milliseconds 600
Click-UIName '启动面板' | Out-Null
Start-Sleep -Milliseconds 1000

$rec = Start-SLRecord -OutFile $out -Seconds $Seconds -Fps 30
$sw.Restart()
"recording -> $out"
Start-Sleep -Milliseconds 800

# 滚动到侧边栏底部，露出「主题工坊」
Set-SidebarScroll 100 | Out-Null
Mark 'scroll-to-workshop'
Start-Sleep -Milliseconds 1200

# 光标移到「主题工坊」再点击（UIA Invoke，光标移动仍会录到）
Click-UIName '主题工坊' -Exact | Out-Null
Mark 'click-workshop'
Start-Sleep -Milliseconds 2800

# 场景网格：光标依次扫过前三张场景卡
foreach ($pt in @(@(620,560),@(1000,560),@(1380,560),@(620,1050),@(1000,1050))) {
    $cr2 = Get-SLClientRect
    [W]::MoveMouse($cr2.X + $pt[0], $cr2.Y + $pt[1])
    Start-Sleep -Milliseconds 700
}
Mark 'hover-scenes'
Start-Sleep -Milliseconds 500

# 点击第一张场景（草原清晨）应用整套主题
$cards = Get-UIElements | Where-Object { $_.Type -eq 'ListItem' -and $_.X -gt 380 }
if ($cards) {
    $first = $cards | Select-Object -First 1
    Click-UIName $first.Name -Exact | Out-Null
    Mark "apply-scene:$($first.Name)"
}
Start-Sleep -Milliseconds 2800

Wait-SLRecord $rec
$marks | Format-Table -AutoSize
if (Test-Path $out) { "OK size=$([math]::Round((Get-Item $out).Length/1MB,2))MB" } else { 'RECORD FAILED' }