param([int]$Seconds = 44)
$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$out = 'D:\SalmonLauncher\promo\public\clips\01-onboarding.mp4'
$w = Set-SLWindow -X 0 -Y 0 -W 1770 -H 1106
"client: $($w.X),$($w.Y) $($w.W)x$($w.H)"
[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
Start-Sleep -Milliseconds 1000

function Wait-Name([string]$Name, [int]$TimeoutMs = 6000) {
    $t0 = Get-Date
    while (((Get-Date) - $t0).TotalMilliseconds -lt $TimeoutMs) {
        $hit = Get-UIElements | Where-Object { $_.Name -and $_.Name.Contains($Name) } | Select-Object -First 1
        if ($hit) { return $true }
        Start-Sleep -Milliseconds 250
    }
    return $false
}

Start-Sleep -Milliseconds 600
$rec = Start-SLRecord -OutFile $out -Seconds $Seconds -Fps 30
"recording -> $out"
Start-Sleep -Milliseconds 1300

# 1) 语言
[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
Move-CursorSmooth -X 634 -Y 643
Start-Sleep -Milliseconds 600
$ok1 = Click-UIName '简体中文' -Mouse
"lang=$ok1"
Start-Sleep -Milliseconds 900
[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
$ok2 = Click-UIName '下一步' -Exact -Mouse
"next1=$ok2"
Start-Sleep -Milliseconds 1800
"terms-step=" + (Wait-Name '我已阅读并同意' 5000)

# 2) 条款
[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
$ok3 = Click-UIName '我已阅读并同意以上条款' -Mouse
"terms=$ok3"
Start-Sleep -Milliseconds 900
$ok4 = Click-UIName '下一步' -Exact -Mouse
"next2=$ok4"
Start-Sleep -Milliseconds 1800
"migrate-step=" + (Wait-Name '迁移' 5000)

# 3) 迁移：直接下一步
[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
$ok5 = Click-UIName '下一步' -Exact -Mouse
"next3=$ok5"
Start-Sleep -Milliseconds 1900
"account-step=" + (Wait-Name '添加离线账户' 5000)

# 4) 账户
[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
$ok6 = Click-UIName '添加离线账户' -Mouse
"addacct=$ok6"
Start-Sleep -Milliseconds 1300
[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
Set-FirstEditValue 'Steve' | Out-Null
Start-Sleep -Milliseconds 800
$ok7 = Click-UIName '确定' -Exact -Mouse
"confirm=$ok7"
Start-Sleep -Milliseconds 1600
$ok8 = Click-UIName '下一步' -Exact -Mouse
"next4=$ok8"
Start-Sleep -Milliseconds 1800
"theme-step=" + (Wait-Name '开始使用' 6000)

# 5) 主题：开始使用
[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
$ok9 = Click-UIName '开始使用' -Mouse
"start=$ok9"
Start-Sleep -Milliseconds 2500
"home=" + (Wait-Name '启动 Minecraft' 6000)

Wait-SLRecord $rec
if (Test-Path $out) { "OK size=$([math]::Round((Get-Item $out).Length/1MB,2))MB" } else { 'RECORD FAILED' }
