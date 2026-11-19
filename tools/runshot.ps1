# Starts the portable app with the given arguments, waits, captures ONLY its window, then closes it.
#   powershell -File runshot.ps1 -Out shot.png -Wait 4 -AppArgs "--theme dark --search taberu"
param(
    [Parameter(Mandatory)] [string]$Out,
    [int]$Wait = 4,
    [string]$Exe = "",
    [int]$Width = 0,
    [int]$Height = 0,
    [string]$AppArgs = ""
)
if (-not $Exe) { $Exe = Join-Path (Split-Path -Parent $PSScriptRoot) "build\portable\Takoboto.exe" }
$argList = @($AppArgs -split "\s+" | Where-Object { $_ })
$p = Start-Process -FilePath $Exe -ArgumentList $argList -PassThru
try {
    Start-Sleep -Seconds $Wait
    $p.Refresh()
    if ($p.HasExited) { "process exited early with code $($p.ExitCode)"; return }
    if ($Width -gt 0) {
        Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class Mv { [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool r); }'
        [Mv]::MoveWindow($p.MainWindowHandle, 40, 40, $Width, $Height, $true) | Out-Null
        Start-Sleep -Seconds 2
    }
    & powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\shot.ps1" -ProcessId $p.Id -Out $Out
} finally {
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
}
