# takoboto_mod_beta

An **unofficial portable desktop edition** of the [Takoboto](https://takoboto.jp) Japanese dictionary:
a classic Windows program (WPF, .NET Framework 4.8) - one folder, no installation, no Store app.
It is a new user interface on top of the original Takoboto dictionary engine.

> Not affiliated with, or endorsed by, the author of Takoboto. Beta.

## What it does

- Instant search: Japanese (kanji, kana, romaji) and English / French / German / Russian; conjugated verbs and adjectives are recognised; matches are highlighted
- A word opens in tabs: **Overview** (writings, reading, meanings with language flags, example phrases) | **Forms** (conjugations) | **Kanji** (readings, meanings, stroke-order diagrams)
- Search as you type: romaji typed letter by letter (`nih` already lists にほ…), kanji mixed with Latin letters (`食b` → 食べ…), and a result list that keeps loading as you scroll (no 32-result cap)
- All text in the word card is selectable; click a word of an example phrase (or select any word) to get a small pop-up with its reading and meaning, click the pop-up to open the entry
- Find kanji by radicals
- History, favorites and your own lists
- Light / dark / "like Windows" theme, switched live; soft grey-green light theme and green accent (easy on the eyes)
- Portable: settings and lists live in a `userdata` folder next to `Takoboto.exe`

## What is NOT in this repository (and why)

The dictionary engine (`Takoboto.Database.dll`), the dictionary data (`takoboto.dat`) and the artwork (mascot, flags)
belong to the author of Takoboto, so they are **not** distributed here. You need your own copy of the original
Takoboto installer (`.AppxBundle` / `.appx`); the script below takes those files from it and builds everything else.

## Build

Requirements: Windows 10/11 and the [.NET SDK](https://dotnet.microsoft.com/download) (8 or newer).
The program itself runs on the .NET Framework 4.8 that ships with Windows.

```powershell
# 1. take the engine, the dictionary and the artwork from YOUR copy of the original installer
powershell -ExecutionPolicy Bypass -File tools\prepare-from-original.ps1 -Original "C:\path\to\Takoboto.AppxBundle"

# 2. build the portable folder (and a zip) -> Takoboto-Portable\Takoboto.exe
powershell -ExecutionPolicy Bypass -File tools\build-portable.ps1
```

## Layout

| Path | What |
|---|---|
| `portable-src/` | the program (WPF): `Views/` pages, `Services/` data layer and theme, `Themes/` colours and styles |
| `tools/build-portable.ps1` | builds and assembles the portable folder |
| `tools/prepare-from-original.ps1` | gets engine, data and artwork from the original installer |
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

**Чего здесь нет:** словарный движок, сам словарь и рисунки принадлежат автору Takoboto, поэтому в репозиторий
они не входят. Нужен ваш собственный установщик оригинального Takoboto: скрипт `tools\prepare-from-original.ps1`
достаёт из него нужные файлы, после чего `tools\build-portable.ps1` собирает программу (команды выше).
