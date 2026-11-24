# Builds the portable edition: assembles <Root>\build\package\Takoboto-Portable, zips it, and updates <Root>\Takoboto-Portable.
#   powershell -ExecutionPolicy Bypass -File build-portable.ps1
# Safe while a copy of the program is running (see step 5).
param([string]$Root = "", [switch]$NoZip, [switch]$Public)   # -Public: the database without the books (what may be published)
$ErrorActionPreference = "Stop"
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }   # the repository folder ($PSScriptRoot is empty inside parameter defaults in PS 5.1)
$tools = Join-Path $Root "tools"
$src = Join-Path $Root "portable-src"
$work = Join-Path $Root "work"                       # everything that is only needed for the work, not part of the program
$stage = Join-Path $work "build\portable-release"
$pkg = Join-Path $work "build\package\Takoboto-Portable"      # assembled here first; the zip is made from it
$out = Join-Path $Root "Takoboto-Portable"                      # the final folder (may be in use!)
$replaced = Join-Path $work "build\replaced"                    # old files that were locked by a running copy
$zip = Join-Path $work "build\Takoboto-Portable.zip"

# a local SDK in work\dotnet-sdk is used when present, otherwise the installed "dotnet" (SDK 8 or newer)
if (Test-Path (Join-Path $work "dotnet-sdk\dotnet.exe")) {
    $env:DOTNET_ROOT = Join-Path $work "dotnet-sdk"
    $env:PATH = "$($env:DOTNET_ROOT);$($env:PATH)"
}
$env:DOTNET_NOLOGO = "1"; $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

function Step($t) { Write-Host "`n=== $t" -ForegroundColor Cyan }

Step "1/5 build (Release, .NET Framework 4.8)"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
dotnet build $src -c Release -o $stage
if ($LASTEXITCODE -ne 0) { throw "build failed" }

Step "2/5 assemble the package"
if (Test-Path $pkg) { Remove-Item $pkg -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $pkg "data") | Out-Null
foreach ($f in "Takoboto.exe", "Takoboto.exe.config") { Copy-Item (Join-Path $stage $f) $pkg }
# the dictionary: our own database (work\database\takoboto.db, made by work\database\build_db.py; -Public: takoboto-public.db without the books)
$db = Join-Path $work ("database\" + $(if ($Public) { "takoboto-public.db" } else { "takoboto.db" }))
if (-not (Test-Path $db)) { throw "The database was not found: $db  (run  python work\database\build_db.py $(if ($Public) { '--public' }))" }
Copy-Item $db (Join-Path $pkg "data\takoboto.db")
# pronunciation: the voice folder (VOICEVOX CORE + the Nemo voices, ~200 MB), kept in <Root>\work\voice-data. Without it the program works, just without speaker buttons.
$voice = Join-Path $work "voice-data"
if (Test-Path (Join-Path $voice "voicevox_core.dll")) { Copy-Item $voice (Join-Path $pkg "voice") -Recurse }
else { Write-Host "NOTE: no voice-data folder: this package has no pronunciation (run tools\get-data.ps1 to fetch it)." -ForegroundColor Yellow }

$readme = @'
TAKOBOTO - portable edition
===========================

Start:   double-click Takoboto.exe   (no installation, no admin rights)
Remove:  delete this folder

The whole program is this one folder. You can copy it anywhere (another disk, a USB stick).
Your settings, history, favorites and lists are saved in the "userdata" folder next to Takoboto.exe.

Files
  Takoboto.exe            the program
  data\takoboto.db        the dictionary: one SQLite database (JMdict, KANJIDIC, KanjiVG, Tatoeba)
  voice\                  pronunciation: VOICEVOX CORE and the VOICEVOX Nemo voices (offline speech synthesis; optional)

Needs Microsoft .NET Framework 4.8, which is already part of Windows 10 (1903 or newer) and Windows 11.

Under the search box: handwriting / radicals / keyboard. No mode is on at first; press a button to open it, press it again to close it.
Handwriting: draw a kanji or kana, tap a candidate (it goes into the search box), press the green check to see the results.
Radicals: pick radicals, click a kanji, press the green check.
The loudspeaker next to a reading and after each example sentence reads it aloud (offline, VOICEVOX Nemo; the female or male voice is chosen in Settings).
The keyboard mode has hiragana and katakana keys (with small kana); while it is open your keyboard types romaji and kana come out.
A word opens in tabs: Overview (writing, reading, meanings, phrases) | Forms | Kanji.
Search works while you type: "nih" already finds the words starting with に-, "食b" finds 食べ...; the list keeps
loading more words as you scroll down.
Every example phrase the dictionary has for a word is shown in the card (more load as you scroll); all offline.
All text in the word card can be selected and copied. Click a word of an example phrase (dotted underline), or
select a word, and a small preview (reading + meaning) pops up; click the preview to open that entry.
Keys:  Ctrl+F or Ctrl+1 search   Ctrl+2 history   Ctrl+3 favorites   Ctrl+4 my lists   Ctrl+5 search by radical   Ctrl+R random word
Theme (light / dark / like Windows), colour scheme (golf green / original red / blue / yellow) and the translation languages (tick English / French / German / Russian; if there is no translation in
the ticked languages, the available ones are shown): Settings.
'@
[System.IO.File]::WriteAllText((Join-Path $pkg "README.txt"), $readme, (New-Object System.Text.UTF8Encoding($true)))

$readmeRu = @'
TAKOBOTO - портативная версия
=============================

Запуск:   двойной клик по Takoboto.exe  (установка и права администратора не нужны)
Удаление: просто удалите эту папку

Вся программа - это одна папка. Её можно скопировать куда угодно (на другой диск, на флешку).
Настройки, история, избранное и ваши списки хранятся в папке "userdata" рядом с Takoboto.exe.

Файлы
  Takoboto.exe            программа
  data\takoboto.db        словарь: одна база данных SQLite (JMdict, KANJIDIC, KanjiVG, Tatoeba)
  voice\                  произношение: VOICEVOX CORE и голоса VOICEVOX Nemo (синтез речи без интернета; необязательно)

Нужен Microsoft .NET Framework 4.8 - он уже входит в Windows 10 (1903 и новее) и Windows 11.

Под строкой поиска: рукописный ввод / радикалы / клавиатура. Сначала ни один режим не включён; нажмите кнопку, чтобы открыть, нажмите ещё раз, чтобы закрыть.
Рукописный ввод: нарисуйте кандзи или кану, нажмите на кандидата (он попадёт в строку поиска), затем зелёную галочку - появятся результаты.
Радикалы: выберите радикалы, нажмите на кандзи, затем зелёную галочку.
Значок динамика у чтения и после каждого примера читает вслух (без интернета, голос VOICEVOX Nemo; женский или мужской - в Settings).
У режима клавиатуры клавиши хираганы и катаканы (с маленькой каной); пока он открыт, ваша клавиатура печатает ромадзи, а получается кана.
Слово открывается во вкладках: Overview (написание, чтение, значения, примеры) | Forms (формы) | Kanji (кандзи).
Поиск работает по мере набора: "nih" сразу находит слова на に-, "食b" находит 食べ...; список подгружается при прокрутке вниз.
Все примеры, которые есть в словаре для слова, показаны прямо в карточке (остальные подгружаются при прокрутке), всё без интернета.
Любой текст в карточке слова можно выделить и скопировать. Нажмите на слово в примере (оно подчёркнуто точками)
или выделите слово - появится маленькая подсказка (чтение и значение); клик по подсказке открывает статью.
Клавиши: Ctrl+F или Ctrl+1 поиск, Ctrl+2 история, Ctrl+3 избранное, Ctrl+4 мои списки, Ctrl+5 поиск по радикалам, Ctrl+R случайное слово.
Тема (светлая / тёмная / как в Windows), окраска (гольф-зелёная / оригинальная красная / синяя / жёлтая) и языки переводов (галочки English / French / German / Russian; если перевода на выбранных языках нет,
показываются те, что есть) - в разделе Settings.
'@
[System.IO.File]::WriteAllText((Join-Path $pkg "README-ru.txt"), $readmeRu, (New-Object System.Text.UTF8Encoding($true)))

Step "3/5 contents"
Get-ChildItem $pkg -Recurse -File | Select-Object @{n = 'Path'; e = { $_.FullName.Substring($pkg.Length + 1) } }, @{n = 'MB'; e = { [math]::Round($_.Length / 1MB, 2) } } | Format-Table -AutoSize

if (-not $NoZip) {
    Step "4/5 zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path $pkg -DestinationPath $zip -CompressionLevel Optimal
    Write-Host ("zip: {0} ({1} MB)" -f $zip, [math]::Round((Get-Item $zip).Length / 1MB, 1))
}

# Put the package into the final folder WITHOUT deleting anything. A running copy locks Takoboto.exe and
# Takoboto.dll: Windows allows moving such a file away, so the old one is moved and the new one takes
# its place (the running copy keeps working, the next start uses the new files). userdata is never touched.
Step "5/5 update the final folder"
New-Item -ItemType Directory -Force $out | Out-Null
foreach ($f in Get-ChildItem $pkg -Recurse -File) {
    $rel = $f.FullName.Substring($pkg.Length + 1)
    $dest = Join-Path $out $rel
    New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
    if (Test-Path $dest) {
        $d = Get-Item $dest
        $same = ($d.Length -eq $f.Length) -and (($f.Length -gt 5MB) -or ((Get-FileHash $dest).Hash -eq (Get-FileHash $f.FullName).Hash))
        if ($same) { Write-Host "  unchanged  $rel"; continue }
    }
    try {
        Copy-Item $f.FullName $dest -Force -ErrorAction Stop
        Write-Host "  updated    $rel"
    } catch {
        New-Item -ItemType Directory -Force $replaced | Out-Null
        Move-Item $dest (Join-Path $replaced ("{0}.{1}" -f (Split-Path $dest -Leaf), [DateTime]::Now.Ticks)) -Force
        Copy-Item $f.FullName $dest -Force
        Write-Host "  replaced   $rel   (the old file was in use and was moved to build\replaced)"
    }
}
Write-Host "`nDONE: $out" -ForegroundColor Green
