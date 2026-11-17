# render-resume.ps1 — 续渲染 + 文件名归一化
# Remotion 分块渲染时文件名位数按块内最大帧号决定（如 element-2772 而非 element-02772），
# 本脚本在每块后把名字统一成 5 位，避免误判"缺失"。
param(
  [int]$Total = 21312,
  [int]$Chunk = 1500,
  [int]$Concurrency = 2,
  [int]$Quality = 88,
  [string]$GL = 'angle',
  [string]$OutDir = 'out/fullseq'
)
$ErrorActionPreference = 'Continue'
Set-Location 'D:\SalmonLauncher\promo'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$full = (Resolve-Path $OutDir).Path

function Normalize {
  $renamed = 0
  foreach ($f in Get-ChildItem $full -Filter 'element-*.jpeg' -File) {
    $numStr = $f.BaseName.Split('-')[1]
    if ($numStr.Length -eq 5) { continue }
    $num = [int]$numStr
    $target = Join-Path $full ("element-{0:D5}.jpeg" -f $num)
    if (Test-Path $target) { Remove-Item $f.FullName -Force; continue }
    Move-Item $f.FullName $target -Force
    $renamed++
  }
  return $renamed
}

function Missing {
  $have = @{}
  foreach ($f in Get-ChildItem $full -Filter 'element-*.jpeg' -File) { $have[[int]$f.BaseName.Split('-')[1]] = $true }
  $miss = @()
  for ($i = 0; $i -lt $Total; $i++) { if (-not $have.ContainsKey($i)) { $miss += $i } }
  return ,$miss
}

function CleanRenderProcs {
  foreach ($p in (Get-CimInstance Win32_Process -Filter "Name='chrome.exe'" -ErrorAction SilentlyContinue)) {
    if ($p.CommandLine -and ($p.CommandLine -match 'headless|remotion|remote-debugging-port')) {
      try { Stop-Process -Id $p.ProcessId -Force -ErrorAction Stop } catch {}
    }
  }
  Start-Sleep -Seconds 2
}

$r = Normalize
"归一化重命名 $r 个文件"
$miss = Missing
"已渲染 $($Total - $miss.Count) / $Total"

for ($try = 0; $try -lt 60 -and $miss.Count -gt 0; $try++) {
  $from = $miss[0]
  # 从第一个缺口开始，取连续的缺口段（最多 Chunk 帧）
  $to = $from
  $idx = 1
  while ($idx -lt $miss.Count -and $miss[$idx] -eq $to + 1 -and ($to - $from + 1) -lt $Chunk) { $to = $miss[$idx]; $idx++ }
  $ram = [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1KB, 0)
  "[$([datetime]::Now.ToString('HH:mm:ss'))] 渲染 $from-$to（$($to - $from + 1) 帧, 并发 $Concurrency, 空闲 ${ram}MB）"
  npx remotion render src/index.ts Film $OutDir --sequence --image-format=jpeg --jpeg-quality=$Quality `
    --concurrency=$Concurrency --gl=$GL --frames="$from-$to" --log=error 2>&1 | Select-Object -Last 4
  $null = Normalize
  $prev = $miss.Count
  $miss = Missing
  "  进度 $($Total - $miss.Count) / $Total（缺口 $($miss.Count)）"
  if ($miss.Count -ge $prev) {
    "  本块无新增帧，清理进程后重试"
    CleanRenderProcs
    Start-Sleep -Seconds 5
  } else {
    CleanRenderProcs
    Start-Sleep -Seconds 2
  }
}

$miss = Missing
"总帧=$Total 已渲染=$($Total - $miss.Count) 缺失=$($miss.Count)"
if ($miss.Count -gt 0 -and $miss.Count -le 60) { "缺失帧: " + ($miss -join ',') }
'done'