$tmp = 'D:\SalmonLauncher\promo\out\audit2'
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$names = @('12-saves','13-logs','14-overview','15-mods')
foreach ($name in $names) {
  $f = "D:\SalmonLauncher\promo\public\clips-v5\$name.mp4"
  if (-not (Test-Path $f)) { Write-Host ("{0}: FILE MISSING" -f $name); continue }
  $dur = [double](ffprobe -v error -show_entries format=duration -of csv=p=0 $f)
  $samples = @(1, [math]::Round($dur*0.5,1), [math]::Round($dur*0.8,1))
  $all = ""
  foreach ($t in $samples) {
    $png = Join-Path $tmp ("$name-$t.png")
    ffmpeg -y -v error -ss $t -i $f -frames:v 1 $png 2>&1 | Out-Null
    if (Test-Path $png) {
      $txt = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\SalmonLauncher\promo\scripts\ocr5.ps1' -Path (Resolve-Path $png).Path 2>&1 | Select-Object -Skip 1) -join ' '
      $all += $txt + " "
    }
  }
  $bad = if ($all -match '回到主界面|弹幕|简介|粉丝|点赞|评论|bilibili|B站|购物|订单|微信|QQ|邮件|Outlook|Trae|旧 应用|deepseel') { 'YES' } else { 'no' }
  $ok = if ($all -match 'SalmonLauncher|启动面板|任务中心|实例|模组|光影|资源包|安装概览|游戏版本|主题工坊|皮肤|联机|存档|日志|偏好|设置') { 'OK' } else { 'MISS' }
  Write-Host ("{0}: 遮挡={1} 期望={2} 片段: {3}" -f $name, $bad, $ok, $all.Substring(0,[math]::Min(180,$all.Length)))
}