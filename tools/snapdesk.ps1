# Photographie la liste des icônes du bureau (même cachée par des fenêtres) et découpe une zone.
param([int]$X, [int]$Y, [int]$W, [int]$H, [string]$Out, [int]$Zoom = 4)
Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public static class D {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
}
"@
[D]::SetProcessDpiAwarenessContext([IntPtr]::new(-4)) | Out-Null
Add-Type -AssemblyName System.Drawing
$list = [IntPtr]::Zero
$prog = [D]::FindWindow("Progman", $null)
$def = [D]::FindWindowEx($prog, [IntPtr]::Zero, "SHELLDLL_DefView", $null)
if ($def -eq [IntPtr]::Zero) {
  [D]::EnumWindows({ param($h, $l) $d = [D]::FindWindowEx($h, [IntPtr]::Zero, "SHELLDLL_DefView", $null); if ($d -ne [IntPtr]::Zero) { $script:def = $d; return $false }; return $true }, [IntPtr]::Zero) | Out-Null
}
$list = [D]::FindWindowEx($def, [IntPtr]::Zero, "SysListView32", $null)
$r = New-Object D+RECT
[D]::GetWindowRect($list, [ref]$r) | Out-Null
$bw = $r.R - $r.L; $bh = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap $bw, $bh
$g = [System.Drawing.Graphics]::FromImage($bmp); $hdc = $g.GetHdc()
[D]::PrintWindow($list, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc); $g.Dispose()
$crop = New-Object System.Drawing.Bitmap ($W * $Zoom), ($H * $Zoom)
$g2 = [System.Drawing.Graphics]::FromImage($crop)
$g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g2.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$src = New-Object System.Drawing.Rectangle ($X - $r.L), ($Y - $r.T), $W, $H
$dst = New-Object System.Drawing.Rectangle 0, 0, ($W * $Zoom), ($H * $Zoom)
$g2.DrawImage($bmp, $dst, $src, [System.Drawing.GraphicsUnit]::Pixel)
$g2.Dispose(); $crop.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
"liste=$list rect=$($r.L),$($r.T),$($r.R),$($r.B)"
