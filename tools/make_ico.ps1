# Builds a multi-size .ico (PNG frames) from the cleaned splash PNG (creature on transparent background).
#   powershell -ExecutionPolicy Bypass -File make_ico.ps1 -Source <cleaned 620x300 png> -Out app.ico
param([Parameter(Mandatory)] [string]$Source, [Parameter(Mandatory)] [string]$Out)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$pf = [System.Drawing.Imaging.PixelFormat]::Format32bppPArgb

$orig = [System.Drawing.Bitmap]::FromFile($Source)
$src = New-Object System.Drawing.Bitmap($orig.Width, $orig.Height, $pf)
$g = [System.Drawing.Graphics]::FromImage($src)
$g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
$g.DrawImage($orig, 0, 0, $orig.Width, $orig.Height); $g.Dispose(); $orig.Dispose()

$minX = $src.Width; $minY = $src.Height; $maxX = 0; $maxY = 0
$bd = $src.LockBits([System.Drawing.Rectangle]::new(0, 0, $src.Width, $src.Height), [System.Drawing.Imaging.ImageLockMode]::ReadOnly, $pf)
$bytes = New-Object byte[] ($bd.Stride * $src.Height)
[System.Runtime.InteropServices.Marshal]::Copy($bd.Scan0, $bytes, 0, $bytes.Length)
$stride = $bd.Stride; $src.UnlockBits($bd)
for ($y = 0; $y -lt $src.Height; $y++) { for ($x = 0; $x -lt $src.Width; $x++) {
    if ($bytes[$y * $stride + $x * 4 + 3] -gt 12) { if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }; if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y } } } }
$bw = $maxX - $minX + 1; $bh = $maxY - $minY + 1; $side = [Math]::Max($bw, $bh)

$master = New-Object System.Drawing.Bitmap($side, $side, $pf)
$g = [System.Drawing.Graphics]::FromImage($master)
$g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
$g.DrawImage($src, [System.Drawing.Rectangle]::new([int](($side - $bw) / 2), [int](($side - $bh) / 2), $bw, $bh),
             [System.Drawing.Rectangle]::new($minX, $minY, $bw, $bh), [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose(); $src.Dispose()

function Frame($px) {
    $cur = $master
    while ($cur.Width -gt $px * 2) {
        $half = [int]($cur.Width / 2)
        $tmp = New-Object System.Drawing.Bitmap($half, $half, $pf)
        $gg = [System.Drawing.Graphics]::FromImage($tmp)
        $gg.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $gg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $gg.DrawImage($cur, 0, 0, $half, $half); $gg.Dispose()
        if ($cur -ne $master) { $cur.Dispose() }
        $cur = $tmp
    }
    $o = New-Object System.Drawing.Bitmap($px, $px, $pf)
    $gg = [System.Drawing.Graphics]::FromImage($o)
    $gg.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $inner = [int][Math]::Round($px * 0.96); $off = [int](($px - $inner) / 2)
    $gg.DrawImage($cur, $off, $off, $inner, $inner); $gg.Dispose()
    if ($cur -ne $master) { $cur.Dispose() }
    $ms = New-Object System.IO.MemoryStream
    $o.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $o.Dispose()
    return ,$ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
$frames = @(); foreach ($s in $sizes) { $frames += ,(Frame $s) }
$ms = New-Object System.IO.MemoryStream; $w = New-Object System.IO.BinaryWriter($ms)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = $frames[$i]
    $w.Write([byte]$(if ($s -ge 256) { 0 } else { $s })); $w.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))
    $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$d.Length); $w.Write([uint32]$offset); $offset += $d.Length
}
foreach ($d in $frames) { $w.Write($d) }
[System.IO.File]::WriteAllBytes($Out, $ms.ToArray())
$master.Dispose()
"icon written: $Out ($($sizes -join ', ') px)"
