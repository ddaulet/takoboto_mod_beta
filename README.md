# takoboto_mod_beta

An **unofficial portable desktop edition** of the [Takoboto](https://takoboto.jp) Japanese dictionary:
a classic Windows program (WPF, .NET Framework 4.8) - one folder, no installation, no Store app.
It is a new user interface on top of the original Takoboto dictionary engine.

> Not affiliated with, or endorsed by, the author of Takoboto. Beta.

## What it does

- Instant search: Japanese (kanji, kana, romaji) and English / French / German / Russian; conjugated verbs and adjectives are recognised; matches are highlighted
- A word opens in tabs: **Overview** (writings, reading, meanings with language flags, example phrases) | **Forms** (conjugations) | **Kanji** (readings, meanings, stroke-order diagrams)
- Search as you type: romaji typed letter by letter (`nih` already lists にほ…), kanji mixed with Latin letters (`食b` → 食べ…), and a result list that keeps loading as you scroll (no 32-result cap)
- Example phrases: every phrase the dictionary has for a word is shown right in the card (no button), loading as you scroll; all of it is offline, taken from the dictionary file
- All text in the word card is selectable; click a word of an example phrase (or select any word) to get a small pop-up with its reading and meaning, click the pop-up to open the entry
- Five input modes under the search box, like Pleco: handwriting (placeholder, later), **search by radical** (the puzzle button: pick radicals, click a kanji to put it into the search box), **keyboard** (default), voice and photo (placeholders)
- On-screen keyboard: ABC, hiragana and katakana layouts (with ゛ ゜ and small-kana keys). In the kana layouts your real keyboard types romaji and kana come out (`nihongo` → にほんご) - no Japanese keyboard needs to be installed
- On the search page every key you press goes into the search box, wherever the focus is
- The search box and the results are in the left column; the word card takes the whole right side
- History, favorites and your own lists
- Translation languages (Settings): tick English / French / German / Russian; where a word, meaning or phrase has nothing in the ticked languages, the languages it does have are shown instead (never an empty meaning); one flag per language, not one per line
- "Random word" button under Search (Ctrl+R): a random entry of the whole dictionary, a new one on every press
- Example phrases like a bilingual book: Japanese on the left, translations on the right, a line down the middle
- Light / dark / "like Windows" theme, switched live; soft grey-green light theme and green accent (easy on the eyes)
- Colour schemes (Settings): Golf green, Original red, Blue, Yellow - independent of light / dark; the logo and the window icon follow the scheme (the icon of the .exe file in Explorer stays green)
- The sidebar is not painted: the colour of the page, divided by thin lines
- Portable: settings and lists live in a `userdata` folder next to `Takoboto.exe`

## Just want to use it?

Download **`Takoboto-Portable.zip`** from the [latest release](https://github.com/ddaulet/takoboto_mod_beta/releases/latest),
unpack it anywhere and run `Takoboto.exe`. Nothing else is needed (Windows 10/11 already has .NET Framework 4.8).

## What is in this repository

Everything the program needs, except one big file:

- the source of the program (`portable-src/`) - mine;
- the original dictionary **engine** `Takoboto.Database.dll` (`portable-src/lib/`), the flags / radical glyph (`portable-src/Assets/`)
  and the mascot artwork - taken unchanged from the original Takoboto, the mascot is only recoloured green;
- the dictionary itself, `takoboto.dat` (107 MB), is above GitHub's 100 MB file limit, so it is **not in git** but in the
  release (inside `Takoboto-Portable.zip`); `tools\get-data.ps1` fetches it for you.

The dictionary *content* is not the app author's work either: it comes from JMdict / EDICT / KANJIDIC (EDRDG), KanjiVG and
Tatoeba, which are published under open (Creative Commons) licences and are credited in the Credits below and on the
program's About page. The engine `Takoboto.Database.dll`, the compiled `takoboto.dat` and the mascot artwork come from the
original Takoboto app.

## Build

Requirements: Windows 10/11 and the [.NET SDK](https://dotnet.microsoft.com/download) (8 or newer).
The program itself runs on the .NET Framework 4.8 that ships with Windows.

```powershell
# 1. get the dictionary (107 MB, downloaded from the release) -> data\takoboto.dat
powershell -ExecutionPolicy Bypass -File tools\get-data.ps1

# 2. build the portable folder (and a zip) -> Takoboto-Portable\Takoboto.exe
powershell -ExecutionPolicy Bypass -File tools\build-portable.ps1
```

`tools\prepare-from-original.ps1 -Original "C:\path\Takoboto.AppxBundle"` is optional: it recreates the engine, the dictionary
and the artwork from your own copy of the original installer.

## Layout

| Path | What |
|---|---|
| `portable-src/` | the program (WPF): `Views/` pages, `Services/` data layer and theme, `Themes/` colours and styles, `lib/` the dictionary engine, `Assets/` flags and logos |
| `tools/build-portable.ps1` | builds and assembles the portable folder |
| `tools/get-data.ps1` | downloads the dictionary (`takoboto.dat`) from the latest release |
| `tools/prepare-from-original.ps1` | optional: recreates engine, data and artwork from the original installer |
| `tools/make_icons.ps1`, `make_ico.ps1`, `recolor.ps1` | icon pipeline (removes the white box, recolours the mascot green) |
| `tools/shot.ps1`, `runshot.ps1`, `drive.ps1` | test helpers: capture the app window, click through the UI via UI Automation |

The app accepts a few switches for testing: `--theme dark|light`, `--page search|history|favorites|lists|kanji|settings|about`,
`--search <text>`, `--radicals a,b`, `--tab overview|forms|kanji`, `--word-test click:<word>|select:<word>`.

## Credits

Dictionary words and kanji: JMdict, EDICT and KANJIDIC (Electronic Dictionary Research and Development Group, www.edrdg.org).
Stroke order: KanjiVG (Ulrich Apel). Example sentences: Tatoeba and the Tanaka Corpus. These come with the original
Takoboto data and are used under their own licences.

---

## По-русски

Неофициальная **портативная версия** японского словаря Takoboto: обычная программа для Windows, одна папка,
без установки и без приложения из Microsoft Store. Это новый интерфейс на оригинальном словарном движке Takoboto.

Что умеет: быстрый поиск (японский, английский, французский, немецкий, русский), карточка слова во вкладках
(Overview / Forms / Kanji), поиск кандзи по радикалам, история, избранное и свои списки, светлая / тёмная тема
(переключается сразу), настройки хранятся в папке `userdata` рядом с программой. Поиск работает по мере набора
(`nih` сразу показывает слова на に…, `食b` находит 食べ…), список подгружается при прокрутке. Текст в карточке можно
выделять; клик по слову в примере (или выделение слова) показывает маленькую подсказку с чтением и значением,
клик по подсказке открывает статью.

**Просто пользоваться:** скачайте `Takoboto-Portable.zip` из [последнего релиза](https://github.com/ddaulet/takoboto_mod_beta/releases/latest),
распакуйте куда угодно и запустите `Takoboto.exe`. Больше ничего не нужно.

**Что в репозитории:** всё необходимое, кроме одного большого файла: исходники программы, оригинальный словарный
движок (`portable-src/lib`), флаги и рисунки (`portable-src/Assets`). Сам словарь `takoboto.dat` (107 МБ) больше лимита
GitHub в 100 МБ на файл, поэтому лежит не в git, а в релизе (внутри `Takoboto-Portable.zip`); `tools\get-data.ps1`
скачивает его сам. Содержимое словаря - не работа автора приложения: это JMdict / EDICT / KANJIDIC (EDRDG), KanjiVG и
Tatoeba под открытыми лицензиями Creative Commons; они указаны в разделе Credits и на странице About программы.
Движок, собранный файл `takoboto.dat` и маскот взяты из оригинального Takoboto. Сборка из исходников: команды выше.
