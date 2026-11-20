# Downloads the dictionary (data\takoboto.dat, 107 MB) that the program needs but that is too big for git.
# It is taken from the ready-made Takoboto-Portable.zip of the latest GitHub release of this project.
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
if (Test-Path $target) { Write-Host "Already there: $target"; return }

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("takoboto_data_" + [Guid]::NewGuid().ToString("N").Substring(0, 8) + ".zip")
try {
    Write-Host "Downloading $Url ..."
    $ProgressPreference = "SilentlyContinue"
    Invoke-WebRequest -Uri $Url -OutFile $tmp -UseBasicParsing
    $zip = [System.IO.Compression.ZipFile]::OpenRead($tmp)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -like "*data/takoboto.dat" -or $_.FullName -like "*data\takoboto.dat" } | Select-Object -First 1
        if (-not $entry) { throw "takoboto.dat is not in the downloaded archive." }
        New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
    } finally { $zip.Dispose() }
    Write-Host "Done: $target" -ForegroundColor Green
    Write-Host "Now build:  powershell -ExecutionPolicy Bypass -File tools\build-portable.ps1"
}
finally { if (Test-Path $tmp) { Remove-Item $tmp -Force -ErrorAction SilentlyContinue } }
