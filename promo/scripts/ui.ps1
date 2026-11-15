# ui.ps1 — 启动器录屏辅助：窗口定位/移动、鼠标点击、UI Automation 元素枚举
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes -ErrorAction SilentlyContinue

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class W {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();

    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    public static IntPtr Find(string titlePart) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, p) => {
            if (!IsWindowVisible(h)) return true;
            StringBuilder sb = new StringBuilder(512);
            GetWindowText(h, sb, sb.Capacity);
            if (sb.ToString().Contains(titlePart)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static void Move(IntPtr h, int x, int y, int w, int ht) {
        ShowWindow(h, 9); // SW_RESTORE
        SetWindowPos(h, IntPtr.Zero, x, y, w, ht, 0x0040); // SWP_SHOWWINDOW
        SetForegroundWindow(h);
    }

    public static int[] ClientScreenRect(IntPtr h) {
        RECT c; GetClientRect(h, out c);
        POINT p = new POINT { X = 0, Y = 0 };
        ClientToScreen(h, ref p);
        return new int[] { p.X, p.Y, c.Right - c.Left, c.Bottom - c.Top };
    }

    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(120);
        mouse_event(0x0002, 0, 0, 0, IntPtr.Zero); // left down
        System.Threading.Thread.Sleep(60);
        mouse_event(0x0004, 0, 0, 0, IntPtr.Zero); // left up
    }

    public static void MoveMouse(int x, int y) { SetCursorPos(x, y); }
}
"@

try { [W]::SetProcessDPIAware() | Out-Null } catch {}

function Get-SLWindow {
    # 必须按进程定位：桌面存在其他标题含 “SalmonLauncher” 的编辑器窗口
    $p = Get-Process -Name 'SL' -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if ($p) { return [IntPtr]$p.MainWindowHandle }
    [W]::Find('SalmonLauncher')
}

function Set-SLWindow([int]$X=0, [int]$Y=0, [int]$W=1770, [int]$H=984) {
    $h = Get-SLWindow
    if ($h -eq [IntPtr]::Zero) { return $null }
    # WinUI 会按最小尺寸回弹，多试几遍直到稳定
    for ($i = 0; $i -lt 8; $i++) {
        [W]::Move($h, $X, $Y, $W, $H)
        Start-Sleep -Milliseconds 450
        $probe = [W]::ClientScreenRect($h)
        if ($probe[3] -le $H - 8) { break }
    }
    Start-Sleep -Milliseconds 300
    $r = [W]::ClientScreenRect($h)
    [pscustomobject]@{Handle=$h; X=$r[0]; Y=$r[1]; W=$r[2]; H=$r[3]}
}

function Get-SLClientRect {
    $h = Get-SLWindow
    if ($h -eq [IntPtr]::Zero) { return $null }
    $r = [W]::ClientScreenRect($h)
    [pscustomobject]@{Handle=$h; X=$r[0]; Y=$r[1]; W=$r[2]; H=$r[3]}
}

# 枚举 UIA 元素：名称/类型/中心点（客户端相对坐标）
function Get-UIElements([switch]$All) {
    $h = Get-SLWindow
    if ($h -eq [IntPtr]::Zero) { return @() }
    $cr = [W]::ClientScreenRect($h)
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
    $cond = [System.Windows.Automation.Condition]::TrueCondition
    $els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    $out = foreach ($e in $els) {
        try {
            $name = $e.Current.Name
            $type = $e.Current.ControlType.ProgrammaticName -replace 'ControlType\.',''
            $r = $e.Current.BoundingRectangle
            if ($r.Width -le 0 -or $r.Height -le 0) { continue }
            if (-not $All -and [string]::IsNullOrWhiteSpace($name)) { continue }
            [pscustomobject]@{
                Name = $name
                Type = $type
                X = [int]($r.X - $cr[0] + $r.Width/2)
                Y = [int]($r.Y - $cr[1] + $r.Height/2)
                W = [int]$r.Width
                H = [int]$r.Height
            }
        } catch {}
    }
    $out
}

# 平滑移动光标（录屏时更自然）
function Move-CursorSmooth([int]$X, [int]$Y, [int]$Steps = 12, [int]$DelayMs = 18) {
    $cr = Get-SLClientRect
    if (-not $cr) { return }
    $tx = $cr.X + $X; $ty = $cr.Y + $Y
    Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public class MP { [DllImport("user32.dll")] public static extern bool GetCursorPos(out P p);
[StructLayout(LayoutKind.Sequential)] public struct P { public int X, Y; } }
"@ -ErrorAction SilentlyContinue
    $p = New-Object 'MP+P'
    [MP]::GetCursorPos([ref]$p) | Out-Null
    $sx = $p.X; $sy = $p.Y
    for ($i = 1; $i -le $Steps; $i++) {
        $t = $i / $Steps
        $e = 1 - [math]::Pow(1 - $t, 2)
        [W]::MoveMouse([int]($sx + ($tx - $sx) * $e), [int]($sy + ($ty - $sy) * $e))
        Start-Sleep -Milliseconds $DelayMs
    }
}

# 按名称点击，-Mouse 强制真实鼠标点击（录屏需要看到光标）
# 匹配优先：名称完全相等 > 可交互控件（ListItem/Button/Hyperlink/RadioButton/CheckBox/ToggleSwitch）> Text
function Click-UIName([string]$Name, [switch]$Exact, [switch]$Mouse) {
    $h = Get-SLWindow
    if ($h -eq [IntPtr]::Zero) { return $false }
    $cr = [W]::ClientScreenRect($h)
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
    $els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $cands = @()
    $interactive = @('ListItem','Button','Hyperlink','RadioButton','CheckBox','ToggleSwitch','ComboBox','MenuItem','TabItem')
    foreach ($e in $els) {
        try {
            $n = $e.Current.Name
            if ([string]::IsNullOrWhiteSpace($n)) { continue }
            $hit = if ($Exact) { $n -eq $Name } else { $n.Contains($Name) }
            if (-not $hit) { continue }
            $r = $e.Current.BoundingRectangle
            if ($r.Width -le 0 -or $r.Height -le 0) { continue }
            $type = $e.Current.ControlType.ProgrammaticName -replace 'ControlType\.',''
            $score = 0
            if ($n -eq $Name) { $score += 100 }
            if ($interactive -contains $type) { $score += 50 }
            if ($type -eq 'Text') { $score -= 20 }
            $score -= [math]::Min(30, [int]($r.Width * $r.Height / 40000))
            $cands += [pscustomobject]@{El=$e; Type=$type; Score=$score; X=[int]($r.X+$r.Width/2); Y=[int]($r.Y+$r.Height/2)}
        } catch {}
    }
    if ($cands.Count -eq 0) { return $false }
    $best = $cands | Sort-Object Score -Descending | Select-Object -First 1
    # 先把光标移到目标上（录屏可见），再用 UIA 模式触发；模式不可用时退回真实点击
    Move-CursorSmooth -X ($best.X - $cr[0]) -Y ($best.Y - $cr[1])
    Start-Sleep -Milliseconds 140
    if (-not $Mouse) {
        $pattern = $null
        if ($best.El.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
            try { $pattern.Invoke(); return $true } catch {}
        }
        $sel = $null
        if ($best.El.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$sel)) {
            try { $sel.Select(); return $true } catch {}
        }
        $tog = $null
        if ($best.El.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$tog)) {
            try { $tog.Toggle(); return $true } catch {}
        }
    }
    [W]::Click($best.X, $best.Y)
    return $true
}

# 客户端相对坐标 → 屏幕绝对坐标点击
function Click-Client([int]$X, [int]$Y) {
    $cr = Get-SLClientRect
    if (-not $cr) { return $false }
    [W]::Click($cr.X + $X, $cr.Y + $Y)
    return $true
}

function Move-Client([int]$X, [int]$Y) {
    $cr = Get-SLClientRect
    if (-not $cr) { return $false }
    [W]::MoveMouse($cr.X + $X, $cr.Y + $Y)
    return $true
}

# 输出元素树文本，便于记录可点击目标
function Show-UI([string]$Filter) {
    $els = Get-UIElements -All
    if ($Filter) { $els = $els | Where-Object { $_.Name -like "*$Filter*" -or $_.Type -like "*$Filter*" } }
    $els | Format-Table -AutoSize
}

# 给第一个 Edit 控件设置文本（ValuePattern）
function Set-FirstEditValue([string]$Value) {
    $h = Get-SLWindow
    if ($h -eq [IntPtr]::Zero) { return $false }
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
    $els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $els) {
        try {
            if ($e.Current.ControlType.ProgrammaticName -ne 'ControlType.Edit') { continue }
            $r = $e.Current.BoundingRectangle
            if ($r.Width -le 0) { continue }
            $vp = $null
            if ($e.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$vp)) {
                $vp.SetValue($Value)
                return $true
            }
            # 回退：点击后用键盘输入
            [W]::Click([int]($r.X + $r.Width/2), [int]($r.Y + $r.Height/2))
            Start-Sleep -Milliseconds 200
            [System.Windows.Forms.SendKeys]::SendWait($Value)
            return $true
        } catch {}
    }
    return $false
}

# 录屏：后台启动 ffmpeg 抓取启动器客户区，返回 job 与输出路径
function Start-SLRecord([string]$OutFile, [int]$Seconds, [int]$Fps = 30) {
    $cr = Get-SLClientRect
    if (-not $cr) { throw '启动器窗口未找到' }
    # 抓取区域必须完整落在屏幕内，否则 gdigrab 直接失败
    $screenW = 1920; $screenH = 1080
    $x = [math]::Max(0, $cr.X); $y = [math]::Max(0, $cr.Y)
    $w = [math]::Min($cr.W, $screenW - $x); $h = [math]::Min($cr.H, $screenH - $y)
    # 偶数尺寸，libx264 yuv420p 要求
    $w = $w - ($w % 2); $h = $h - ($h % 2)
    if (Test-Path $OutFile) { Remove-Item $OutFile -Force }
    $args = @(
        '-y','-f','gdigrab','-framerate',"$Fps",
        '-offset_x',"$x",'-offset_y',"$y",
        '-video_size',"${w}x${h}",
        '-draw_mouse','1','-i','desktop',
        '-t',"$Seconds",
        '-c:v','libx264','-preset','ultrafast','-crf','16','-pix_fmt','yuv420p',
        '-movflags','+faststart', $OutFile
    )
    $job = Start-Job -ScriptBlock { param($ff,$a) & $ff @a 2>&1 } -ArgumentList 'ffmpeg',$args
    [pscustomobject]@{Job=$job; File=$OutFile; Rect=[pscustomobject]@{X=$x;Y=$y;W=$w;H=$h}}
}

function Wait-SLRecord($Rec) {
    $Rec.Job | Wait-Job -Timeout 600 | Out-Null
    $out = $Rec.Job | Receive-Job
    Remove-Job $Rec.Job -Force -ErrorAction SilentlyContinue
    $out | Select-Object -Last 3
}