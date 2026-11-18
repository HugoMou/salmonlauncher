# apply-theme.ps1 — 应用草原倾城主题
$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$h = Get-SLWindow
[W]::SetWindowPos($h, [IntPtr](-1), 0, 0, 1936, 1152, 0x0040) | Out-Null
Start-Sleep -Milliseconds 300
[W]::SetWindowPos($h, [IntPtr](-1), 0, 0, 1936, 1152, 0x0040) | Out-Null
Start-Sleep -Milliseconds 300
[W]::ShowWindow($h, 3) | Out-Null
Start-Sleep -Milliseconds 500
Set-SLTopMost -On:$true
Start-Sleep -Milliseconds 300
[W]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 500

Nav '主题工坊' 100 | Out-Null; Start-Sleep -Milliseconds 1600

# 点击"草原倾城"按钮
$root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
foreach ($e in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
  try {
    if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' -and $e.Current.Name -match '草原') {
      $inv = $null
      if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$inv)) { $inv.Invoke(); break }
    }
  } catch {}
}
Start-Sleep -Milliseconds 3000
"已应用草原倾城主题"