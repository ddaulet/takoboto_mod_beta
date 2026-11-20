# OPTIONAL: (re)creates the engine, the dictionary and the artwork from YOUR OWN copy of the original Takoboto installer.
# The repository already contains the engine (portable-src\lib) and the artwork (portable-src\Assets, app.ico); the
# dictionary comes from tools\get-data.ps1. Use this script only if you want to rebuild them from the original installer:
#
#   extracted\app\Takoboto.Database.dll            engine
#   extracted\app\Takoboto.Database\takoboto.dat   dictionary
#   portable-src\Assets\*.png, portable-src\app.ico  flags, radical glyph, green logos and the icon
#
#   powershell -ExecutionPolicy Bypass -File tools\prepare-from-original.ps1 -Original "C:\path\Takoboto.AppxBundle"
param(
    [Parameter(Mandatory)] [string]$Original,                 # the .AppxBundle / .appx / .msix of the original Takoboto
    [string]$Root = ""
)
$ErrorActionPreference = "Stop"
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }   # ($PSScriptRoot is empty inside parameter defaults in Windows PowerShell 5.1)
Add-Type -AssemblyName System.IO.Compression.FileSystem

$tools = Join-Path $Root "tools"
$app = Join-Path $Root "extracted\app"
$assets = Join-Path $Root "portable-src\Assets"
$ps = "powershell"
$psArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File")

function Expand-Zip($zip, $dir) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zip, $dir)
}
function Run($script, [string[]]$more) {
    & $ps @psArgs (Join-Path $tools $script) @more | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "$script failed" }
}
function Step($t) { Write-Host "`n=== $t" -ForegroundColor Cyan }

if (-not (Test-Path -LiteralPath $Original)) { throw "File not found: $Original" }
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("takoboto_prepare_" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
New-Item -ItemType Directory $tmp | Out-Null
try {
    Step "1/3 unpacking the original installer"
    $outer = Join-Path $tmp "outer"
    Expand-Zip $Original $outer
    # a bundle contains the real package as a (big) .appx/.msix; a plain package has the files directly
    $inner = Get-ChildItem $outer -Recurse -File -Include *.appx, *.msix | Sort-Object Length -Descending | Select-Object -First 1
    if ($inner) { Expand-Zip $inner.FullName $app; Write-Host "  package: $($inner.Name)" }
    else { if (Test-Path $app) { Remove-Item $app -Recurse -Force }; New-Item -ItemType Directory -Force (Split-Path $app) | Out-Null; Copy-Item $outer $app -Recurse }

    $need = @("Takoboto.Database.dll", "Takoboto.Database\takoboto.dat", "Assets\FlagEn.png", "Assets\FlagFr.png",
              "Assets\FlagDe.png", "Assets\FlagRu.png", "Assets\Kanji\A.png", "Assets\takoboto_620x300.png")
    foreach ($n in $need) { if (-not (Test-Path (Join-Path $app $n))) { throw "Not found in the installer: $n (is this the original Takoboto package?)" } }
    Write-Host "  engine and dictionary: ok"
    New-Item -ItemType Directory -Force (Join-Path $Root "portable-src\lib") | Out-Null
    Copy-Item (Join-Path $app "Takoboto.Database.dll") (Join-Path $Root "portable-src\lib\Takoboto.Database.dll") -Force
    New-Item -ItemType Directory -Force (Join-Path $Root "data") | Out-Null
    Copy-Item (Join-Path $app "Takoboto.Database\takoboto.dat") (Join-Path $Root "data\takoboto.dat") -Force

    Step "2/3 artwork: flags, radical glyph"
    New-Item -ItemType Directory -Force $assets | Out-Null
    foreach ($f in "FlagEn.png", "FlagFr.png", "FlagDe.png", "FlagRu.png") { Copy-Item (Join-Path $app "Assets\$f") (Join-Path $assets $f) -Force }
    Copy-Item (Join-Path $app "Assets\Kanji\A.png") (Join-Path $assets "RadicalA.png") -Force

    Step "3/3 icon and logos (the mascot is re-coloured green)"
    # the original image has a white box painted behind the mascot: remove it, then recolour
    $clean = Join-Path $tmp "clean"; New-Item -ItemType Directory $clean | Out-Null
    Run "make_icons.ps1" @("-Source", (Join-Path $app "Assets\takoboto_620x300.png"), "-OutDir", $clean)
    $cleanPng = Join-Path $clean "takoboto_620x300.png"
    $green = Join-Path $tmp "green.png"; $mint = Join-Path $tmp "mint.png"
    Run "recolor.ps1" @("-In", $cleanPng, "-Out", $green, "-Hue", "148", "-Sat", "0.5", "-Light", "0.78")
    Run "recolor.ps1" @("-In", $cleanPng, "-Out", $mint, "-Hue", "150", "-Sat", "0.45", "-Light", "1.35")
    foreach ($v in @(@("g", $green, "Logo.png"), @("m", $mint, "LogoLight.png"))) {
        $d = Join-Path $tmp ("logo_" + $v[0]); New-Item -ItemType Directory $d | Out-Null
        Run "make_icons.ps1" @("-Source", $v[1], "-OutDir", $d, "-SkipClean")
        Copy-Item (Join-Path $d "takoboto_30x30.scale-400.png") (Join-Path $assets $v[2]) -Force
    }
    Run "make_ico.ps1" @("-Source", $green, "-Out", (Join-Path $Root "portable-src\app.ico"))

    Write-Host "`nDone. Now build the program:" -ForegroundColor Green
    Write-Host "  powershell -ExecutionPolicy Bypass -File tools\build-portable.ps1"
}
finally {
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
}
