# Downloads what the program needs but what is too big for git: the dictionary (data\takoboto.dat, 107 MB) and the
# pronunciation files (voice-data\, ~190 MB: VOICEVOX CORE and the VOICEVOX Nemo voices).
# Both are taken from the ready-made Takoboto-Portable.zip of the latest GitHub release of this project.
#   powershell -ExecutionPolicy Bypass -File tools\get-data.ps1
param(
    [string]$Root = "",
    [string]$Url = "https://github.com/ddaulet/takoboto_mod_beta/releases/latest/download/Takoboto-Portable.zip"
)
$ErrorActionPreference = "Stop"
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }   # ($PSScriptRoot is empty inside parameter defaults in Windows PowerShell 5.1)
Add-Type -AssemblyName System.IO.Compression.FileSystem
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$target = Join-Path $Root "data\takoboto.dat"
$voiceDir = Join-Path $Root "voice-data"
$haveDat = Test-Path $target
$haveVoice = Test-Path (Join-Path $voiceDir "voicevox_core.dll")
if ($haveDat -and $haveVoice) { Write-Host "Already there: $target and $voiceDir"; return }

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("takoboto_data_" + [Guid]::NewGuid().ToString("N").Substring(0, 8) + ".zip")
try {
    Write-Host "Downloading $Url ..."
    $ProgressPreference = "SilentlyContinue"
    Invoke-WebRequest -Uri $Url -OutFile $tmp -UseBasicParsing
    $zip = [System.IO.Compression.ZipFile]::OpenRead($tmp)
    try {
        if (-not $haveDat) {
            $entry = $zip.Entries | Where-Object { $_.FullName -like "*data/takoboto.dat" -or $_.FullName -like "*data\takoboto.dat" } | Select-Object -First 1
            if (-not $entry) { throw "takoboto.dat is not in the downloaded archive." }
            New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
            Write-Host "Done: $target" -ForegroundColor Green
        }
        if (-not $haveVoice) {
            # the entries  Takoboto-Portable/voice/...  go to  voice-data\...
            $n = 0
            foreach ($e in $zip.Entries) {
                if ($e.FullName -match '^[^\\/]+[\\/]voice[\\/](.+)$' -and $e.Name) {
                    $dest = Join-Path $voiceDir ($Matches[1] -replace '/', '\')
                    New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
                    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($e, $dest, $true)
                    $n++
                }
            }
            if ($n -eq 0) { Write-Host "The downloaded archive has no pronunciation files (the program works without them)." -ForegroundColor Yellow }
            else { Write-Host "Done: $voiceDir ($n files)" -ForegroundColor Green }
        }
    } finally { $zip.Dispose() }
    Write-Host "Now build:  powershell -ExecutionPolicy Bypass -File tools\build-portable.ps1"
}
finally { if (Test-Path $tmp) { Remove-Item $tmp -Force -ErrorAction SilentlyContinue } }
