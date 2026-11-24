# Drives the portable app through Windows UI Automation (the same interface screen readers use) to test real clicks.
#   powershell -File drive.ps1 -Steps "click:Favorites;shot:fav.png;click:Settings;..." [-AppArgs "--theme light"]
# Step kinds:  click:<Name>   type:<Name>=<text>   wait:<ms>   shot:<file>   popup:<file> (smallest window)   select:<n>  (n-th list row)
#              cardscroll:<percent> (scroll the word card)   scrollinfo (position of every scrollable area)   sel (print the selected text of every text block)   scrollend (scroll the first list to its end)   drag:x1,y1,x2,y2 (mouse drag, window coordinates)
param(
    [Parameter(Mandatory)] [string]$Steps,
    [string]$AppArgs = "",
    [string]$ShotDir = "",
    [string]$Exe = ""
)
if (-not $Exe) { $Exe = Join-Path (Split-Path -Parent $PSScriptRoot) "Takoboto-Portable\Takoboto.exe" }
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$argList = @($AppArgs -split "\s+" | Where-Object { $_ })
$p = Start-Process -FilePath $Exe -ArgumentList $argList -PassThru
function Find-Window {
    for ($i = 0; $i -lt 40; $i++) {
        $w = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst("Children",
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)))
        if ($w) { return $w }
        Start-Sleep -Milliseconds 250
    }
    throw "window not found"
}
function Find-El($root, $name) {
    $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $el = $root.FindFirst("Descendants", $c)
    if ($el) { return $el }
    # fall back to a contains-match (names with special characters, popups)
    $all = $root.FindAll("Descendants", [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) { if ($e.Current.Name -and $e.Current.Name.Contains($name)) { return $e } }
    return $null
}
try {
    $win = Find-Window
    Start-Sleep -Seconds 3
    foreach ($step in ($Steps -split ";" | Where-Object { $_.Trim() })) {
        $kind, $arg = $step.Trim() -split ":", 2
        switch ($kind) {
            "wait" { Start-Sleep -Milliseconds ([int]$arg) }
            "click" {
                $el = Find-El $win $arg
                if (-not $el) { "!! not found: $arg"; continue }
                $pat = $null
                if ($el.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pat)) { $pat.Invoke() }
                elseif ($el.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pat)) { $pat.Select() }
                elseif ($el.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pat)) { $pat.Toggle() }
                else { "!! cannot click: $arg" }
                Start-Sleep -Milliseconds 700
            }
            "type" {
                $n, $t = $arg -split "=", 2
                $el = Find-El $win $n
                if (-not $el) { "!! not found: $n"; continue }
                $pat = $null
                if ($el.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pat)) { $pat.SetValue($t) } else { "!! no value pattern: $n" }
                Start-Sleep -Milliseconds 900
            }
            "select" {
                $rows = $win.FindAll("Descendants", (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
                $vis = @($rows | Where-Object { -not $_.Current.IsOffscreen })
                $idx = [int]$arg
                if ($idx -lt $vis.Count) { $pat = $vis[$idx].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern); $pat.Select() } else { "!! no row $idx (have $($vis.Count))" }
                Start-Sleep -Milliseconds 900
            }
            "sel" {
                $docs = $win.FindAll("Descendants", (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Document)))
                foreach ($dd in $docs) {
                    $tp = $null
                    if ($dd.TryGetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern, [ref]$tp)) {
                        foreach ($r in $tp.GetSelection()) { $t = $r.GetText(200); if ($t) { "selection: [$t]" } }
                    }
                }
                "docs: $($docs.Count)"
            }
            "mclick" {
                # a REAL mouse click (cursor + button events) at the centre of a named element, e.g. mclick:Settings   or   mclick:食事@-10,0  (offset in pixels)
                Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class Mc { [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y); [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, IntPtr e); [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte sc, uint fl, UIntPtr ex); }' -ErrorAction SilentlyContinue
                $nm, $off = $arg -split "@", 2
                $el = Find-El $win $nm
                if (-not $el) { "!! not found: $nm"; continue }
                [Mc]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [Mc]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
                $p.Refresh(); [void][Mc]::SetForegroundWindow($p.MainWindowHandle); Start-Sleep -Milliseconds 300
                $b = $el.Current.BoundingRectangle
                $ox = 0; $oy = 0; if ($off) { $o = $off -split ","; $ox = [int]$o[0]; $oy = [int]$o[1] }
                [void][Mc]::SetCursorPos([int]($b.X + $b.Width / 2 + $ox), [int]($b.Y + $b.Height / 2 + $oy)); Start-Sleep -Milliseconds 300
                [Mc]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 80; [Mc]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)
                Start-Sleep -Milliseconds 900
            }
            "scrollinfo" {
                $all = $win.FindAll("Descendants", [System.Windows.Automation.Condition]::TrueCondition)
                foreach ($e in $all) {
                    $sp = $null
                    if ($e.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$sp) -and $sp.Current.VerticallyScrollable) {
                        "scroll: {0} '{1}' at {2:N1}% (view {3:N1}% of the content)" -f $e.Current.ControlType.ProgrammaticName, $e.Current.Name, $sp.Current.VerticalScrollPercent, $sp.Current.VerticalViewSize
                    }
                }
            }
            "cardscroll" {
                # scroll the word card (the scrollable area that is NOT the result list) to <arg> percent
                $all = $win.FindAll("Descendants", [System.Windows.Automation.Condition]::TrueCondition)
                foreach ($e in $all) {
                    if ($e.Current.ControlType -eq [System.Windows.Automation.ControlType]::List) { continue }
                    $sp = $null
                    if ($e.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$sp) -and $sp.Current.VerticallyScrollable) { $sp.SetScrollPercent(-1, [double]$arg); break }
                }
                Start-Sleep -Milliseconds 1200
            }
            "value" {
                $el = Find-El $win $arg
                if (-not $el) { "!! not found: $arg"; continue }
                $pat = $null
                if ($el.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pat)) { "value of '$arg': [" + $pat.Current.Value + "]" } else { "!! no value pattern: $arg" }
            }
            "scrollend" {
                $lst = $win.FindFirst("Descendants", (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::List)))
                if ($lst) { $sp = $lst.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern); $sp.SetScrollPercent(-1, 100); Start-Sleep -Milliseconds 1500 } else { "!! no list" }
            }
            "drag" {
                Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class Mouse { [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y); [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, IntPtr e); [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r); [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte sc, uint fl, UIntPtr ex); [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; } }' -ErrorAction SilentlyContinue
                $p.Refresh(); $r = New-Object Mouse+RECT; [void][Mouse]::GetWindowRect($p.MainWindowHandle, [ref]$r)
                [Mouse]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [Mouse]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)   # a tap of Alt lets us bring our test window to the front
                [void][Mouse]::SetForegroundWindow($p.MainWindowHandle); Start-Sleep -Milliseconds 400
                $c = $arg -split "," | ForEach-Object { [int]$_ }
                [void][Mouse]::SetCursorPos($r.L + $c[0], $r.T + $c[1]); Start-Sleep -Milliseconds 150
                # relative moves (flag 1) between the down and the up: WPF needs real mouse-move messages while the button is held
                [Mouse]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 200          # left down
                $steps = 12; $dx = [int](($c[2]-$c[0]) / $steps); $dy = [int](($c[3]-$c[1]) / $steps)
                foreach ($k in 1..$steps) { [Mouse]::mouse_event(1, $dx, $dy, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60 }
                Start-Sleep -Milliseconds 150
                [Mouse]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 1200         # left up
            }
            "popup" { $f = if ($ShotDir) { Join-Path $ShotDir $arg } else { $arg }; & powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\shot.ps1" -ProcessId $p.Id -Out $f -Smallest }
            "shot" { $f = if ($ShotDir) { Join-Path $ShotDir $arg } else { $arg }; & powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\shot.ps1" -ProcessId $p.Id -Out $f }
            "dump" {
                $all = $win.FindAll("Descendants", [System.Windows.Automation.Condition]::TrueCondition)
                $all | Where-Object { $_.Current.Name } | Select-Object -First ([int]$arg) | ForEach-Object { "{0} | {1}" -f $_.Current.ControlType.ProgrammaticName, $_.Current.Name }
            }
        }
    }
} finally {
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
}
