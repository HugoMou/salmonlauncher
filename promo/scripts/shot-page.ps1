# shot-page.ps1 — 用启动器内置渲染树截图（--capture-page），不受桌面遮挡/冻结影响
# 用法: shot-page.ps1 -Pages 'launch.home,inst.mods' [-OutDir path] [-Onboard 0..5]
param(
  [Parameter(Mandatory=$true)][string]$Pages,
  [string]$OutDir = 'D:\SalmonLauncher\promo\public\shots-v3',
  [string]$Onboard = ''
)
$ErrorActionPreference = 'Continue'
$exe = 'D:\SalmonLauncher\src\SL.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\SL.exe'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$pageList = $Pages -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
$onbList = $Onboard -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }

# 关闭已有实例，避免单实例锁冲突
Get-Process -Name 'SL' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

if ($onbList.Count -gt 0) {
  foreach ($s in $onbList) {
    & $exe --capture $OutDir --capture-page "onboard-$s" | Out-Null
    $p = Get-Process -Name 'SL' -ErrorAction SilentlyContinue
    if ($p) { $p.WaitForExit(60000) | Out-Null }
    $f = Join-Path $OutDir "onboard-$s.png"
    if (Test-Path $f) { "onboard-$s OK ($([math]::Round((Get-Item $f).Length/1KB))KB)" } else { "onboard-$s FAILED" }
  }
}

foreach ($pg in $pageList) {
  & $exe --capture $OutDir --capture-page $pg | Out-Null
  $p = Get-Process -Name 'SL' -ErrorAction SilentlyContinue
  if ($p) { $p.WaitForExit(60000) | Out-Null }
  $name = "page-$($pg.Replace('.','-')).png"
  $f = Join-Path $OutDir $name
  if (Test-Path $f) { "$pg OK ($([math]::Round((Get-Item $f).Length/1KB))KB) -> $name" } else { "$pg FAILED" }
}