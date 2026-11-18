# audit-clips.ps1 — 逐段 OCR 核查录屏素材是否截错（被遮挡 / 画面不对 / 无变化）
param([string]$Dir = 'D:\SalmonLauncher\promo\public\clips-v5')
$ErrorActionPreference = 'Continue'
$tmp = 'D:\SalmonLauncher\promo\out\audit'
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

$EXPECT = @{
  '01-onboarding' = 'SalmonLauncher'
  '02-install'    = '安装概览|游戏版本|加载器|任务中心'
  '03-launch'     = '启动|实例|启动 Minecraft'
  '04-game'       = 'Minecraft|Singleplayer|Options'
  '05-tour'       = 'SalmonLauncher|启动面板|任务中心|概览|实例设置|实例日志'
  '06-tour2'      = '游戏版本|加载器|模组仓库|光影仓库|资源包仓库'
  '07-theme'      = '主题工坊|内置场景'
  '08-shaders'    = '光影仓库'
  '09-packs'      = '资源包仓库'
  '10-skins'      = '皮肤'
  '11-online'     = '联机|Campfire'
  '12-saves'      = '存档'
  '13-logs'       = '实例日志'
  '14-overview'   = '实例概览'
  '15-mods'       = '模组仓库'
  '16-install'    = '安装概览|游戏版本'
}
$BAD = '回到主界面|弹幕|简介|粉丝|点赞|评论|bilibili|B站|购物|订单|微信|QQ|邮件|Outlook|Trae'

foreach ($f in (Get-ChildItem $Dir -Filter '*.mp4' | Where-Object { $_.Name -notlike '_*' } | Sort-Object Name)) {
  $base = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
  $key = ($base -replace '^\d+-\w+(-\w+)?$', { param($m) $m.Value }) # 保持原名
  $key = $base -replace '^\d+-', ''
  $key = ($key -split '-')[0]
  $dur = [double](ffprobe -v error -show_entries format=duration -of csv=p=0 $f.FullName)
  $samples = @(1, [math]::Round($dur*0.25,1), [math]::Round($dur*0.5,1), [math]::Round($dur*0.78,1)) | ForEach-Object { [math]::Max(0.5, [math]::Min($_, $dur-0.5)) } | Select-Object -Unique
  $texts = @()
  $pngs = @()
  foreach ($t in $samples) {
    $png = Join-Path $tmp ("$base-$t.png")
    ffmpeg -y -v error -ss $t -i $f.FullName -frames:v 1 $png 2>&1 | Out-Null
    if (Test-Path $png) {
      $txt = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\SalmonLauncher\promo\scripts\ocr5.ps1' -Path $png 2>&1 | Select-Object -Skip 1) -join ' '
      $texts += $txt
      $pngs += $png
    }
  }
  $all = $texts -join ' '
  $badHit = if ($all -match $BAD) { 'YES' } else { 'no' }
  $exp = $EXPECT[$key]
  $okHit = if ($exp -and $all -match $exp) { 'OK' } elseif ($exp) { 'MISS' } else { '-' }
  # 画面变化度（首末帧 SSIM，太低=静止）
  $diff = ''
  if ($pngs.Count -ge 2) {
    $s = ffmpeg -y -i $pngs[0] -i $pngs[-1] -lavfi ssim -f null - 2>&1 | Select-String 'All:'
    if ($s -match 'All:([0-9.]+)') { $diff = [math]::Round([double]$Matches[1], 3) }
  }
  "{0,-22} dur={1,5:N0}s 遮挡={2,-3} 期望={3,-4} SSIM={4}" -f $base, $dur, $badHit, $okHit, $diff
  if ($badHit -eq 'YES') {
    "    片段: $($all.Substring(0,[math]::Min(160,$all.Length)))"
  }
}
$tmp