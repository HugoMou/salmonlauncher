# render-chunks.ps1 — 分块渲染（Remotion 浏览器偶发崩溃 / 内存不足时的稳妥方案）
# 每块渲染后清理子进程并等待，失败自动重试；最终统计缺失帧
param(
  [int]$Total = 21312,
  [int]$Chunk = 2000,
  [int]$Concurrency = 2,
  [int]$Quality = 88,
  [string]$GL = 'angle',
  [string]$OutDir = 'out/fullseq'
)
$ErrorActionPreference = 'Continue'
Set-Location 'D:\SalmonLauncher\promo'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Have([int]$i) {
  Test-Path (Join-Path $OutDir ("element-{0:D5}.jpeg" -f $i))
}

function CleanRenderProcs {
  # 只清理无头 chrome（Remotion 启动）与超时未退出的 node，避免误杀用户程序
  $procs = Get-CimInstance Win32_Process -Filter "Name='chrome.exe'" -ErrorAction SilentlyContinue
  foreach ($p in $procs) {
    if ($p.CommandLine -and ($p.CommandLine -match 'headless|remotion|remote-debugging-port')) {
      try { Stop-Process -Id $p.ProcessId -Force -ErrorAction Stop } catch {}
    }
  }
  Start-Sleep -Seconds 2
}

$start = 0
while ($start -lt $Total) {
  $end = [Math]::Min($start + $Chunk - 1, $Total - 1)
  $need = @()
  for ($i = $start; $i -le $end; $i++) { if (-not (Have $i)) { $need += $i } }
  if ($need.Count -eq 0) { $start = $end + 1; continue }

  $from = ($need | Measure-Object -Minimum).Minimum
  $to = ($need | Measure-Object -Maximum).Maximum
  $ok = $false
  for ($try = 1; $try -le 4; $try++) {
    $ram = [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1KB, 0)
    "[$([datetime]::Now.ToString('HH:mm:ss'))] 渲染 $from-$to 第 $try 次（并发 $Concurrency, 空闲内存 ${ram}MB）"
    npx remotion render src/index.ts Film $OutDir --sequence --image-format=jpeg --jpeg-quality=$Quality `
      --concurrency=$Concurrency --gl=$GL --frames="$from-$to" --log=error 2>&1 | Select-Object -Last 6
    $missing = 0
    for ($i = $from; $i -le $to; $i++) { if (-not (Have $i)) { $missing++ } }
    if ($missing -eq 0) { "  OK"; $ok = $true; break }
    "  缺 $missing 帧，重试（先清理进程）"
    CleanRenderProcs
    Start-Sleep -Seconds 6
  }
  if (-not $ok) { "  !! 块 $from-$to 未完成" }
  $start = $end + 1
  CleanRenderProcs
  Start-Sleep -Seconds 3
}

$missAll = @()
for ($i = 0; $i -lt $Total; $i++) { if (-not (Have $i)) { $missAll += $i } }
"总帧=$Total 已渲染=$($Total - $missAll.Count) 缺失=$($missAll.Count)"
if ($missAll.Count -gt 0 -and $missAll.Count -le 60) { "缺失帧: " + ($missAll -join ',') }
'done'