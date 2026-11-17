# rec-game-menu.ps1 — 录 45 秒 Minecraft 主菜单画面（游戏已启动并位于主菜单）
param([int]$Seconds = 45, [string]$Out = 'D:\SalmonLauncher\promo\public\clips-v3\04b-game-menu.mp4')
$ErrorActionPreference = 'Continue'

Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices; using System.Text;
public class GM {
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
 public static string Title(){ var sb=new StringBuilder(512); GetWindowText(GetForegroundWindow(), sb, sb.Capacity); return sb.ToString(); }
 public static string Fit(IntPtr h){
   ShowWindow(h, 9);
   SetWindowPos(h, IntPtr.Zero, 100, 100, 1296, 759, 0x0040);
   SetForegroundWindow(h);
   System.Threading.Thread.Sleep(1200);
   RECT c; GetClientRect(h, out c); POINT p = new POINT(); ClientToScreen(h, ref p);
   return p.X + "," + p.Y + " " + (c.Right-c.Left) + "x" + (c.Bottom-c.Top);
 }
 public static void Hover(int x, int y){ SetCursorPos(x,y); System.Threading.Thread.Sleep(400); }
}
"@ -ErrorAction SilentlyContinue

$g = Get-Process -Name 'javaw','java' -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $g) { 'NO GAME WINDOW'; exit 1 }
$gh = [IntPtr]$g.MainWindowHandle
$rect = [GM]::Fit($gh)
"game client: $rect"
"foreground: '" + [GM]::Title() + "'"
Start-Sleep -Seconds 2

# 解析客户区屏幕坐标
$parts = $rect -split '[ ,x]+'
$ox = [int]$parts[0]; $oy = [int]$parts[1]; $cw = [int]$parts[2]; $ch = [int]$parts[3]
# 偶数化（libx264 yuv420p 要求）
$cw = $cw - ($cw % 2); $ch = $ch - ($ch % 2)

$out = $Out
if (Test-Path $out) { Remove-Item $out -Force }
$args = @('-y','-f','gdigrab','-framerate','30','-offset_x',"$ox",'-offset_y',"$oy",
  '-video_size',"${cw}x${ch}",'-draw_mouse','1','-i','desktop','-t',"$Seconds",
  '-c:v','libx264','-preset','ultrafast','-crf','16','-pix_fmt','yuv420p','-movflags','+faststart',$out)
$job = Start-Job -ScriptBlock { param($ff,$a) & $ff @a 2>&1 } -ArgumentList 'ffmpeg',$args
Start-Sleep -Milliseconds 900

# 光标在菜单按钮间平滑移动（视觉上演示"可操作"）
$targets = @(
  @([int]($cw*0.5), [int]($ch*0.36)),
  @([int]($cw*0.5), [int]($ch*0.46)),
  @([int]($cw*0.5), [int]($ch*0.56)),
  @([int]($cw*0.5), [int]($ch*0.46)),
  @([int]($cw*0.34), [int]($ch*0.62)),
  @([int]($cw*0.66), [int]($ch*0.62))
)
for ($i = 0; $i -lt [math]::Floor($Seconds / 2.2); $i++) {
  $t = $targets[$i % $targets.Count]
  [GM]::Hover($ox + $t[0], $oy + $t[1])
  Start-Sleep -Milliseconds 1400
}
$job | Wait-Job -Timeout 180 | Out-Null
$job | Receive-Job | Select-Object -Last 2
Remove-Job $job -Force -ErrorAction SilentlyContinue
if (Test-Path $out) { "OK $([math]::Round((Get-Item $out).Length/1MB,2)) MB" } else { 'RECORD FAILED' }