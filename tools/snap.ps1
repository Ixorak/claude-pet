# Capture d'une zone de l'écran (pixels physiques) en PNG, pour vérifier le rendu de Pet.
param([int]$X, [int]$Y, [int]$W, [int]$H, [string]$Out, [double]$Zoom = 1.0)
Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public static class Snap {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
  [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
  [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr d, int x, int y, int w, int h, IntPtr s, int sx, int sy, uint rop);
}
"@
[Snap]::SetProcessDpiAwarenessContext([IntPtr]::new(-4)) | Out-Null
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$src = [Snap]::GetDC([IntPtr]::Zero)
[Snap]::BitBlt($hdc, 0, 0, $W, $H, $src, $X, $Y, 0x40CC0020) | Out-Null
[Snap]::ReleaseDC([IntPtr]::Zero, $src) | Out-Null
$g.ReleaseHdc($hdc); $g.Dispose()
if ($Zoom -ne 1.0) {
  $w2 = [int]($W * $Zoom); $h2 = [int]($H * $Zoom)
  $b2 = New-Object System.Drawing.Bitmap $w2, $h2
  $g2 = [System.Drawing.Graphics]::FromImage($b2)
  $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
  $g2.DrawImage($bmp, 0, 0, $w2, $h2); $g2.Dispose(); $bmp.Dispose(); $bmp = $b2
}
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
