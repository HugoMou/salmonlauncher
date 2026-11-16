# rec-gameplay.ps1 — 实机录制：启动游戏 → 进单人世界 → 走动/转向 → 录制
param([int]$PlaySeconds = 45, [int]$WorldWait = 90)
$ErrorActionPreference = 'Continue'
. "$PSScriptRoot\ui.ps1"

$outDir = 'D:\SalmonLauncher\promo\public\clips-v3'
$tmp = Join-Path $outDir '_t'
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices; using System.Text;
public class GX {
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
 [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
 [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
 [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; }
 [StructLayout(LayoutKind.Sequential)] public struct KI { public ushort vk, scan; public uint flags, time; public IntPtr extra; }
 [StructLayout(LayoutKind.Sequential)] public struct IN { public uint type; public KI ki; }
 [DllImport("user32.dll")] public static extern uint SendInput(uint n, IN[] p, int size);

 public static string Title() { var sb=new StringBuilder(512); GetWindowText(GetForegroundWindow(), sb, sb.Capacity); return sb.ToString(); }
 public static void Activate(IntPtr h) { ShowWindow(h, 9); SetWindowPos(h, IntPtr.Zero, 0, 0, 1296, 759, 0x0040); SetForegroundWindow(h); }
 public static string Rect(IntPtr h) {
   RECT c; GetClientRect(h, out c); POINT p = new POINT(); ClientToScreen(h, ref p);
   return p.X + "," + p.Y + " " + (c.Right-c.Left) + "x" + (c.Bottom-c.Top);
 }
 public static void Click(int x, int y) { SetCursorPos(x,y); System.Threading.Thread.Sleep(200); mouse_event(0x0002,0,0,0,IntPtr.Zero); System.Threading.Thread.Sleep(120); mouse_event(0x0004,0,0,0,IntPtr.Zero); }
 public static void KeyDown(ushort vk) { var a=new IN[1]; a[0].type=1; a[0].ki.vk=vk; SendInput(1,a,Marshal.SizeOf(typeof(IN))); }
 public static void KeyUp(ushort vk) { var a=new IN[1]; a[0].type=1; a[0].ki.vk=vk; a[0].ki.flags=2; SendInput(1,a,Marshal.SizeOf(typeof(IN))); }
 public static void Press(ushort vk) { KeyDown(vk); System.Threading.Thread.Sleep(60); KeyUp(vk); }
}
"@ -ErrorAction SilentlyContinue

function Shot([string]$name) {
  $f = Join-Path $tmp "$name.png"
  ffmpeg -y -v error -f gdigrab -framerate 1 -offset_x 11 -offset_y 45 -video_size 1274x702 -i desktop -update 1 -frames:v 1 $f 2>&1 | Out-Null
  return $f
}
function Ocr([string]$file) {
  (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\ocr5.ps1" -Path $file 2>&1 | Select-Object -Skip 1) -join ' '
}

# ---------- 1) 启动游戏 ----------
$g = Get-Process -Name 'javaw','java' -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $g) {
  [W]::ShowWindow((Get-SLWindow), 3) | Out-Null
  Start-Sleep -Seconds 2
  [W]::SetForegroundWindow((Get-SLWindow)) | Out-Null
  Start-Sleep -Milliseconds 600
  $cr = Get-SLClientRect
  "launcher client=$($cr.X),$($cr.Y) $($cr.W)x$($cr.H)"
  Click-UIName '启动面板' -Exact | Out-Null
  Start-Sleep -Seconds 2
  [GX]::Click($cr.X + 829, $cr.Y + 562)
  "launch clicked"
  for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Seconds 3
    $g = Get-Process -Name 'javaw' -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if ($g) { "game window after $((($i+1)*3))s"; break }
  }
}
if (-not $g) { 'GAME NOT RUNNING'; exit 1 }
$gh = [IntPtr]$g.MainWindowHandle
[GX]::Activate($gh)
Start-Sleep -Seconds 4
"game client: " + [GX]::Rect($gh)
"foreground: " + [GX]::Title()

# ---------- 2) 主菜单 → 单人游戏 ----------
Start-Sleep -Seconds 3
$f = Shot 'menu'
"menu ocr: " + (Ocr $f)
# 键盘：Tab 聚焦首个控件（Singleplayer），Enter 进入
[GX]::Press(0x09)
Start-Sleep -Milliseconds 500
[GX]::Press(0x0D)
Start-Sleep -Seconds 4
$f = Shot 'world-select'
$txt = Ocr $f
"after tab+enter: $txt"

if ($txt -notmatch 'world|World|世界|Create|创建|Play|单人') {
  # 鼠标兜底：点常见按钮位置
  foreach ($yy in 389, 462, 530, 601) {
    [GX]::Click(648, 45 + $yy)
    Start-Sleep -Seconds 3
    $f = Shot "try-$yy"
    $t2 = Ocr $f
    "click y=$yy -> $t2"
    if ($t2 -match 'world|World|Create|Play') { $txt = $t2; break }
  }
}

# ---------- 3) 创建新世界 ----------
$f = Shot 'create-check'
$t = Ocr $f
"state: $t"
if ($t -match 'Create New World|创建新的世界|创建新世界') {
  # 世界列表页右下通常是「创建新的世界」
  [GX]::Click(648, 45 + 601)
  Start-Sleep -Seconds 3
  $f = Shot 'create-screen'
  "create screen: " + (Ocr $f)
  # 再点一次确认创建（按钮在左下）
  [GX]::Click(400, 45 + 610)
  Start-Sleep -Seconds 3
}

# ---------- 4) 等待世界生成 ----------
$inWorld = $false
for ($i = 0; $i -lt [math]::Ceiling($WorldWait / 5); $i++) {
  Start-Sleep -Seconds 5
  $f = Shot "wait-$i"
  $t = Ocr $f
  if ($i % 3 -eq 0) { "  [$($i*5)s] $t" }
  # 进世界后不再有菜单文字，画面以场景为主
  if ($t -match '') { }
  # 用画面亮度和文字判断：世界加载后基本无 UI 文字
  if (($t -notmatch 'Create|Play|Options|Quit|Select World|单人|多人') -and ($t.Trim().Length -lt 30)) { $inWorld = $true; "进入世界（$($i*5)s）"; break }
}

# ---------- 5) 录制实机画面 ----------
$file = Join-Path $outDir '08-gameplay.mp4'
if (Test-Path $file) { Remove-Item $file -Force }
Set-SLTopMost -On:$false
[GX]::SetForegroundWindow($gh) | Out-Null
Start-Sleep -Milliseconds 500
$rec = Start-SLRecord -OutFile $file -Seconds $PlaySeconds -Fps 30
"录制实机 $PlaySeconds s ..."
Start-Sleep -Milliseconds 800
# 转视角 + 走动
for ($i = 0; $i -lt 5; $i++) {
  [GX]::KeyDown(0x57)   # W
  Start-Sleep -Milliseconds 1500
  [GX]::KeyUp(0x57)
  [GX]::SetCursorPos(700, 400) | Out-Null
  [GX]::mouse_event(0x0001, 40, 0, 0, [IntPtr]::Zero)
  Start-Sleep -Milliseconds 1200
  [GX]::mouse_event(0x0001, -80, 0, 0, [IntPtr]::Zero)
  Start-Sleep -Milliseconds 900
  [GX]::Press(0x1B)     # ESC (avoid opening pause menu too early: press only once later)
  Start-Sleep -Milliseconds 400
  [GX]::Press(0x1B)
  Start-Sleep -Milliseconds 600
}
Wait-SLRecord $rec | Out-Null
if (Test-Path $file) { "08 OK $([math]::Round((Get-Item $file).Length/1MB,2)) MB" } else { '08 FAILED' }
