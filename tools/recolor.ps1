# Re-colours the red octopus: hue -> green, keeps the neutral pixels (white of the eye, black pupil) and the alpha.
#   powershell -File recolor.ps1 -In red.png -Out green.png [-Hue 150] [-Sat 0.45] [-Light 0.8]
# Every saturated pixel keeps its lightness *relationship* (outline darker than body); Sat / Light scale S and L.
param(
    [Parameter(Mandatory)] [string]$In,
    [Parameter(Mandatory)] [string]$Out,
    [double]$Hue = 150,      # degrees (green)
    [double]$Sat = 0.45,     # saturation multiplier
    [double]$Light = 0.8     # lightness multiplier
)
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class Recolor
{
    public static void Run(string inp, string outp, double hue, double sat, double light)
    {
        byte[] px; int w, h, stride;
        using (Bitmap bmp = new Bitmap(inp))
        {
            w = bmp.Width; h = bmp.Height;
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            stride = d.Stride; px = new byte[stride * h];
            Marshal.Copy(d.Scan0, px, 0, px.Length);
            bmp.UnlockBits(d);
        }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                if (px[i + 3] == 0) continue;
                double r = px[i + 2] / 255.0, g = px[i + 1] / 255.0, b = px[i] / 255.0;
                double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
                double l = (mx + mn) / 2, s = 0;
                if (mx != mn) s = l > 0.5 ? (mx - mn) / (2 - mx - mn) : (mx - mn) / (mx + mn);
                if (s < 0.12) continue;                       // white / black / grey stays as it is
                double nl = Math.Min(1, l * light), ns = Math.Min(1, s * sat);
                double q = nl < 0.5 ? nl * (1 + ns) : nl + ns - nl * ns, p = 2 * nl - q, hk = hue / 360.0;
                px[i + 2] = (byte)Math.Round(255 * Hue2Rgb(p, q, hk + 1.0 / 3));
                px[i + 1] = (byte)Math.Round(255 * Hue2Rgb(p, q, hk));
                px[i] = (byte)Math.Round(255 * Hue2Rgb(p, q, hk - 1.0 / 3));
            }
        using (Bitmap o = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            BitmapData d = o.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(px, 0, d.Scan0, px.Length);
            o.UnlockBits(d);
            o.Save(outp, ImageFormat.Png);
        }
    }
    static double Hue2Rgb(double p, double q, double t)
    {
        if (t < 0) t += 1; if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 0.5) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
}
"@
[Recolor]::Run($In, $Out, $Hue, $Sat, $Light)
"recoloured -> $Out"
