# record-onboarding.ps1 — 录制首次引导全流程
param([int]$Seconds = 42)
$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$out = 'D:\SalmonLauncher\promo\public\clips\01-onboarding.mp4'
# 引导各步最小高度不同，统一固定到最大高度，避免录制中窗口跳动
$w = Set-SLWindow -X 0 -Y 0 -W 1770 -H 1106
"client: $($w.X),$($w.Y) $($w.W)x$($w.H)"
# 录屏抓的是屏幕像素：必须让启动器窗口置顶，否则会录到别的窗口
[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
Start-Sleep -Milliseconds 900

# 起始画面停留，然后开始录
Start-Sleep -Milliseconds 800
$rec = Start-SLRecord -OutFile $out -Seconds $Seconds -Fps 30
"recording -> $out ($Seconds s)"
Start-Sleep -Milliseconds 1200

# 1) 选择语言
Move-CursorSmooth -X 634 -Y 643
Start-Sleep -Milliseconds 700
Click-UIName '简体中文' -Mouse | Out-Null
Start-Sleep -Milliseconds 1100
Click-UIName '下一步' -Exact -Mouse | Out-Null
Start-Sleep -Milliseconds 1600

# 2) 条款
Click-UIName '我已阅读并同意以上条款' -Mouse | Out-Null
Start-Sleep -Milliseconds 900
Click-UIName '下一步' -Exact -Mouse | Out-Null
Start-Sleep -Milliseconds 1600

# 3) 迁移：直接下一步跳过
Click-UIName '下一步' -Exact -Mouse | Out-Null
Start-Sleep -Milliseconds 1700

# 4) 账户：添加离线账户
[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
Click-UIName '添加离线账户' -Mouse | Out-Null
Start-Sleep -Milliseconds 1200
[W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
Set-FirstEditValue 'Steve' | Out-Null
Start-Sleep -Milliseconds 800
Click-UIName '确定' -Exact -Mouse | Out-Null
Start-Sleep -Milliseconds 1500
Click-UIName '下一步' -Exact -Mouse | Out-Null
Start-Sleep -Milliseconds 1600

# 5) 主题：直接开始使用（保持默认配色）
Click-UIName '开始使用' -Mouse | Out-Null
Start-Sleep -Milliseconds 2200

Wait-SLRecord $rec
if (Test-Path $out) {
    $f = Get-Item $out
    "OK size=$([math]::Round($f.Length/1MB,2))MB"
} else { 'RECORD FAILED' }