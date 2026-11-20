# Captures the largest visible top-level window of ONE process (not the whole screen) to a PNG, using PrintWindow.
#   powershell -File shot.ps1 -ProcessId 1234 -Out C:\path\shot.png
param(
    [Parameter(Mandatory)] [int]$ProcessId,
    [Parameter(Mandatory)] [string]$Out,
    [switch]$Smallest     # capture the smallest visible window (e.g. a pop-up) instead of the main one
)
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
public static class WinShot
{
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    public static string Shot(int pid, string path, bool smallest)
    {
        IntPtr best = IntPtr.Zero; long bestArea = smallest ? long.MaxValue : 0;
        EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == (uint)pid && IsWindowVisible(h))
            {
                RECT r; GetWindowRect(h, out r);
                long a = (long)(r.R - r.L) * (r.B - r.T);
                if (smallest ? (a > 3000 && a < bestArea) : (a > bestArea)) { bestArea = a; best = h; }
            }
            return true;
        }, IntPtr.Zero);
        if (best == IntPtr.Zero) return "no visible window";
        RECT rr; GetWindowRect(best, out rr);
        int w = rr.R - rr.L, hh = rr.B - rr.T;
        using (Bitmap b = new Bitmap(w, hh))
        {
            using (Graphics g = Graphics.FromImage(b)) { IntPtr dc = g.GetHdc(); PrintWindow(best, dc, 2); g.ReleaseHdc(dc); }
            b.Save(path, ImageFormat.Png);
        }
        return "captured " + w + "x" + hh;
    }
}
"@
[WinShot]::Shot($ProcessId, $Out, [bool]$Smallest)
