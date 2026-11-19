# Icon pipeline for Takoboto Plus.
#   1. removes the baked-in white rectangle from the splash image (flood fill from the outside + "colour to alpha"
#      on the anti-aliased edge, so there is no white fringe; the white of the eye is enclosed and stays)
#   2. cuts the creature out and writes crisp square logos for every display scale (no plate, no background)
#   powershell -ExecutionPolicy Bypass -File make_icons.ps1 -Source <620x300.png> -OutDir <Assets dir>
param(
    [Parameter(Mandatory)] [string]$Source,
    [Parameter(Mandatory)] [string]$OutDir,
    [switch]$SkipClean   # the source already has a transparent background (e.g. a recoloured octopus): do not run the white-removal step
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class Cleaner
{
    // Bounding box of the visible pixels only (no changes to the image).
    public static string Bbox(string srcPath)
    {
        using (Bitmap bmp = new Bitmap(srcPath))
        {
            int minX = bmp.Width, minY = bmp.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    if (bmp.GetPixel(x, y).A > 12) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            return string.Format("cleared=0, edge=0, bbox={0},{1},{2},{3}", minX, minY, maxX, maxY);
        }
    }

    // Removes the outer near-white / transparent area. Returns "cleared=..., edge=..., bbox=x0,y0,x1,y1".
    public static string Clean(string srcPath, string dstPath)
    {
        int w, h, stride;
        byte[] px;
        using (Bitmap bmp = new Bitmap(srcPath))
        {
            w = bmp.Width; h = bmp.Height;
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            stride = d.Stride;
            px = new byte[stride * h];
            Marshal.Copy(d.Scan0, px, 0, px.Length);
            bmp.UnlockBits(d);
        }
        bool[] region = new bool[w * h];
        Stack<int> st = new Stack<int>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if ((x == 0 || y == 0 || x == w - 1 || y == h - 1) && IsBg(px, stride, x, y)) { region[y * w + x] = true; st.Push(y * w + x); }
        int[] dx = { 1, -1, 0, 0 }; int[] dy = { 0, 0, 1, -1 };
        while (st.Count > 0)
        {
            int p = st.Pop(); int x = p % w, y = p / w;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + dx[k], ny = y + dy[k];
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int q = ny * w + nx;
                if (!region[q] && IsBg(px, stride, nx, ny)) { region[q] = true; st.Push(q); }
            }
        }
        // anti-aliased edge: up to 2 pixels outside the region
        bool[] edge = new bool[w * h];
        bool[] prev = region;
        for (int pass = 0; pass < 2; pass++)
        {
            bool[] next = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int p = y * w + x;
                    if (region[p] || edge[p]) continue;
                    for (int oy = -1; oy <= 1 && !next[p]; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int nx = x + ox, ny = y + oy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            if (prev[ny * w + nx]) { next[p] = true; break; }
                        }
                }
            for (int i = 0; i < next.Length; i++) if (next[i]) edge[i] = true;
            prev = next;
        }
        long cleared = 0, edged = 0;
        int minX = w, minY = h, maxX = -1, maxY = -1;
        byte[] outpx = new byte[stride * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4, p = y * w + x;
                byte b = px[i], g = px[i + 1], r = px[i + 2], a = px[i + 3];
                if (region[p]) { cleared++; continue; }            // transparent black
                if (edge[p] && a > 0)
                {
                    int mn = Math.Min(b, Math.Min(g, r));
                    double t = (255.0 - mn) / 255.0;                // "colour to alpha" against white
                    if (t < 0.02) { cleared++; continue; }
                    if (t < 0.999)
                    {
                        edged++;
                        b = Clamp((b - 255.0 * (1 - t)) / t); g = Clamp((g - 255.0 * (1 - t)) / t); r = Clamp((r - 255.0 * (1 - t)) / t);
                        a = (byte)Math.Round(255.0 * t * a / 255.0);
                    }
                }
                outpx[i] = b; outpx[i + 1] = g; outpx[i + 2] = r; outpx[i + 3] = a;
                if (a > 12) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            }
        using (Bitmap o = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            BitmapData d = o.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(outpx, 0, d.Scan0, outpx.Length);
            o.UnlockBits(d);
            o.Save(dstPath, ImageFormat.Png);
        }
        return string.Format("cleared={0}, edge={1}, bbox={2},{3},{4},{5}", cleared, edged, minX, minY, maxX, maxY);
    }
    static byte Clamp(double v) { return (byte)(v < 0 ? 0 : (v > 255 ? 255 : Math.Round(v))); }
    static bool IsBg(byte[] px, int stride, int x, int y)
    {
        int i = y * stride + x * 4;
        if (px[i + 3] < 16) return true;
        return Math.Min(px[i], Math.Min(px[i + 1], px[i + 2])) >= 236;   // opaque near-white
    }
}
"@

$pf = [System.Drawing.Imaging.PixelFormat]::Format32bppPArgb   # premultiplied alpha: no light fringe when resampling

# ---- 1. clean the splash source -------------------------------------------------------------------------------------
$clean = Join-Path ([System.IO.Path]::GetTempPath()) "takoboto_clean_620x300.png"
if ($SkipClean) { Copy-Item $Source $clean -Force; $info = [Cleaner]::Bbox($clean) } else { $info = [Cleaner]::Clean($Source, $clean) }
Write-Host "splash cleaned: $info"
$box = ($info -split "bbox=")[1] -split "," | ForEach-Object { [int]$_ }
$minX = $box[0]; $minY = $box[1]; $maxX = $box[2]; $maxY = $box[3]
$bw = $maxX - $minX + 1; $bh = $maxY - $minY + 1
Write-Host "creature bounding box: $bw x $bh px"

# the splash screen itself: same 620x300 canvas, creature only
Copy-Item $clean (Join-Path $OutDir "SplashScreen.scale-100.png") -Force
Copy-Item $clean (Join-Path $OutDir "takoboto_620x300.png") -Force

# ---- 2. square master of the creature ---------------------------------------------------------------------------------
$orig = [System.Drawing.Bitmap]::FromFile($clean)
$src = New-Object System.Drawing.Bitmap($orig.Width, $orig.Height, $pf)
$g = [System.Drawing.Graphics]::FromImage($src)
$g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
$g.DrawImage($orig, 0, 0, $orig.Width, $orig.Height)
$g.Dispose(); $orig.Dispose()

$side = [Math]::Max($bw, $bh)
$master = New-Object System.Drawing.Bitmap($side, $side, $pf)
$g = [System.Drawing.Graphics]::FromImage($master)
$g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
$g.DrawImage($src, [System.Drawing.Rectangle]::new([int](($side - $bw) / 2), [int](($side - $bh) / 2), $bw, $bh),
             [System.Drawing.Rectangle]::new($minX, $minY, $bw, $bh), [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose(); $src.Dispose()

function Resize-Square($img, [int]$boxPx, [double]$fill) {
    $inner = [int][Math]::Round($boxPx * $fill)   # the creature occupies $fill of the box, centred
    $cur = $img
    while ($cur.Width -gt $inner * 2) {           # halve step by step while far above the target (keeps small icons crisp)
        $half = [int]($cur.Width / 2)
        $tmp = New-Object System.Drawing.Bitmap($half, $half, $pf)
        $gg = [System.Drawing.Graphics]::FromImage($tmp)
        $gg.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $gg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $gg.DrawImage($cur, 0, 0, $half, $half); $gg.Dispose()
        if ($cur -ne $img) { $cur.Dispose() }
        $cur = $tmp
    }
    $out = New-Object System.Drawing.Bitmap($boxPx, $boxPx, $pf)
    $gg = [System.Drawing.Graphics]::FromImage($out)
    $gg.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $gg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $off = [int](($boxPx - $inner) / 2)
    $gg.DrawImage($cur, $off, $off, $inner, $inner); $gg.Dispose()
    if ($cur -ne $img) { $cur.Dispose() }
    return $out
}

# (file base name, size in px at 100 %, how much of the box the creature fills)
$sets = @(
    @{ name = "takoboto_30x30";   size = 30;  fill = 0.94 },   # taskbar / Start list / Alt-Tab
    @{ name = "takoboto_50x50";   size = 50;  fill = 0.90 },   # store logo shown in Settings > Apps
    @{ name = "takoboto_150x150"; size = 150; fill = 0.82 }    # Start tile
)
$scales = 100, 125, 150, 200, 400
foreach ($s in $sets) {
    foreach ($sc in $scales) {
        $px = [int][Math]::Round($s.size * $sc / 100)
        $bmp = Resize-Square $master $px $s.fill
        $bmp.Save((Join-Path $OutDir ("{0}.scale-{1}.png" -f $s.name, $sc)), [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    }
    Write-Host ("  {0}: scales {1}" -f $s.name, ($scales -join ","))
}
$bmp = Resize-Square $master 70 0.90
$bmp.Save((Join-Path $OutDir "takoboto_70x70.png"), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
$master.Dispose()
