# Drives the portable app through Windows UI Automation (the same interface screen readers use) to test real clicks.
#   powershell -File drive.ps1 -Steps "click:Favorites;shot:fav.png;click:Settings;..." [-AppArgs "--theme light"]
# Step kinds:  click:<Name>   type:<Name>=<text>   wait:<ms>   shot:<file>   select:<n>  (n-th list row)
param(
    [Parameter(Mandatory)] [string]$Steps,
    [string]$AppArgs = "",
    [string]$ShotDir = "",
    [string]$Exe = ""
)
if (-not $Exe) { $Exe = Join-Path (Split-Path -Parent $PSScriptRoot) "build\portable\Takoboto.exe" }
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
