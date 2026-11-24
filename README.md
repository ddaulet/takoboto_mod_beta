# takoboto_mod_beta

An **unofficial portable desktop edition** of the [Takoboto](https://takoboto.jp) Japanese dictionary:
a classic Windows program (WPF, .NET Framework 4.8) - one folder, no installation, no Store app.
It is a new program with its own search engine on top of one SQLite database made from the open dictionary data (JMdict, KANJIDIC, KanjiVG, Tatoeba): the dictionary is a single file, `data\takoboto.db`, and more dictionaries can be added to it.

> Not affiliated with, or endorsed by, the author of Takoboto. Beta.

## What it does

- Instant search: Japanese (kanji, kana, romaji) and English / French / German / Russian; conjugated verbs and adjectives are recognised; matches are highlighted
- A word opens in tabs: **Overview** (writings, reading, meanings with language flags, example phrases) | **Forms** (conjugations) | **Kanji** (readings, meanings, stroke-order diagrams)
- Search as you type: romaji typed letter by letter (`nih` already lists にほ…), kanji mixed with Latin letters (`食b` → 食べ…), and a result list that keeps loading as you scroll (no 32-result cap)
- Furigana (the reading in small kana, over EACH kanji: 学[がっ]校[こう], centred above it; words whose reading belongs to the whole word, like 今日 きょう, get it centred over the word) in the example sentences, switched on / off in Settings. A reading is shown only when it is certain: the dictionary reading is aligned with the written form, and where that is ambiguous or wrong for the sentence (来る -> き / こ, 良い -> よ, a counter after a number, a lone kanji at the end of a compound) the kanji are left without
- Select a word (or click a word of an example sentence): a pop-up shows it; a spelling or reading that several words share (こうこう = 高校, 孝行, 航行 ...) lists ALL of them, and the pop-up opens upwards when the mouse is near the bottom of the screen
- The empty search page suggests 6 random common words, different every time; Settings: Random word can be limited to common words (many example sentences - the dictionary has no frequency list) and can skip words written in katakana
- Example sentences: every sentence the dictionary has for a word (the card shows how many there are) is shown right in the card (no button), loading as you scroll; all of it is offline, taken from the dictionary file
- All text in the word card is selectable; click a word of an example phrase (or select any word) to get a small pop-up with its reading and meaning, click the pop-up to open the entry
- Three input modes under the search box, like Pleco: **handwriting** (pen), **search by radical** (puzzle) and **keyboard**. No mode is on at first (you just type); press a mode button to open it, press it again to close it
- Search by radical fills the results area: a bar on top (the picked radicals in one row | round X | round green check), the kanji that contain them below - a click puts the kanji into the search box at once - then the radicals. X clears the picked radicals, the green check closes the panel and shows the results
- Handwriting: write ONE kanji on a square pad with the mouse (or a pen); the candidates appear in the bar on top (one row, scrolls sideways, then a round X that clears the pad and a round green check), a click on one puts it into the search box; a round button in the top right corner of the pad undoes the last stroke. Works offline: the stroke templates are built from the stroke-order data (KanjiVG) that is in the dictionary file, about 5,000 kanji, the first time it is opened (a few seconds; kept in `userdata\handwriting.bin`). Strokes in the wrong order and a missing stroke are tolerated. Hiragana and katakana too (their strokes are a small text file, `portable-src/Assets/kana-strokes.txt`, from KanjiVG); a candidate goes into the search box selected and the next candidate replaces it until you click into the box; nothing is searched until you press the green check
- Keyboard mode: an on-screen kana keyboard (hiragana / katakana, with the small kana ぁ ぃ ぅ ぇ ぉ ゃ ゅ ょ っ ゎ as keys, and ゛ ゜); while it is open your real keyboard types romaji and kana come out (`nihongo` → にほんご) - no Japanese keyboard needs to be installed
- Pronunciation: a loudspeaker next to the reading and after every Japanese example sentence reads it aloud, **offline** (speech synthesis by [VOICEVOX CORE](https://github.com/VOICEVOX/voicevox_core), voices "VOICEVOX Nemo", "VOICEVOX:青山龍星" (a rough samurai man) "VOICEVOX:玄野武宏" (a young anime hero) and "VOICEVOX:ずんだもん" (a bright kawaii girl); the voice and one of three speeds - fast, medium, slow - are chosen in Settings). The voice files are the folder `voice` next to `Takoboto.exe` (about 200 MB); without it the program works and simply has no loudspeaker buttons
- On the search page every key you press goes into the search box, wherever the focus is
- The search box and the results are in the left column; the word card takes the whole right side
- Back / forward arrows at the left of the tab strip of the word card (also Alt+Left / Alt+Right and the side buttons of the mouse): a click on a word in a sentence, on the random word or on a result opens another card, and one click brings the previous card back - with the search and the list it came from
- Grammar page (Ctrl+6): a list of grammar constructions with meanings, connection patterns, explanations and example sentences, and a Grammar block on the word card that links to it. The data are tables of the same database, made from your own grammar books; they are not part of this repository or of the download (the public database has none), and without them the page is simply not shown
- History, favorites and your own lists
- Translation languages (Settings): tick English / French / German / Russian; where a word, meaning or phrase has nothing in the ticked languages, the languages it does have are shown instead (never an empty meaning); one flag per language (in the meanings: once for the whole word), not one per line
- "Random word" button under Search (Ctrl+R): a random entry of the whole dictionary, a new one on every press
- Example sentences like a bilingual book: Japanese on the left, translations on the right, a line down the middle
- Light / dark / "like Windows" theme, switched live; soft grey-green light theme and green accent (easy on the eyes)
- Colour schemes (Settings): Golf green, Original red, Blue, Yellow - independent of light / dark; the logo and the window icon follow the scheme (the icon of the .exe file in Explorer stays green)
- The sidebar is not painted: the colour of the page, divided by thin lines
- Portable: settings and lists live in a `userdata` folder next to `Takoboto.exe`

## Just want to use it?

There is no download yet: the program needs the dictionary file `data\takoboto.db`, and the first public release (the program
and the dictionary in one zip) is still to come. Until then build it yourself (below).

## What is in this repository

The source of the program, and nothing else:

- the program (`portable-src/`): the WPF user interface and its own data layer (`portable-src/Data/`: a small SQLite reader and
  the word / kanji / sentence search) - mine;
- the flags, the radical glyph and the mascot artwork (`portable-src/Assets/`): the mascot comes from the original Takoboto,
  recoloured green;
- the helper scripts (`tools/`).

The dictionary itself is **not** in git: `data\takoboto.db` is one SQLite file made from the open dictionary data (JMdict,
KANJIDIC, KanjiVG, Tatoeba), and it is not published in this repository. The pronunciation files (the `voice` folder next to
`Takoboto.exe`, about 200 MB: VOICEVOX CORE, ONNX Runtime, the OpenJTalk dictionary and the voice models) are not in git either.
The dictionary *content* is not the app author's work: it comes from JMdict / EDICT / KANJIDIC (EDRDG), KanjiVG and Tatoeba,
which are published under open (Creative Commons) licences and are credited in the Credits below and on the program's About page.

## Build

Requirements: Windows 10/11 and the [.NET SDK](https://dotnet.microsoft.com/download) (8 or newer).
The program itself runs on the .NET Framework 4.8 that ships with Windows.

```powershell
# builds the program and assembles the portable folder -> Takoboto-Portable\Takoboto.exe
powershell -ExecutionPolicy Bypass -File tools\build-portable.ps1
```

The script takes the dictionary from `work\database\takoboto.db` (`-Public` takes `takoboto-public.db`, the database without
the grammar books) and the voice files from `workoice-data`; both folders are local and not part of the repository. Without
the voice files the program works, just without the speaker buttons.

## Layout

| Path | What |
|---|---|
| `portable-src/` | the program (WPF): `Views/` pages, `Services/` interface helpers, themes and the grammar / sentence code, `Data/` the SQLite data layer and the search, `Themes/` colours and styles, `Assets/` flags and logos |
| `tools/build-portable.ps1` | builds the program and assembles the portable folder (and a zip) |
| `tools/make_icons.ps1`, `make_ico.ps1`, `recolor.ps1` | icon pipeline (removes the white box, recolours the mascot green) |
| `tools/shot.ps1`, `runshot.ps1`, `drive.ps1` | test helpers: capture the app window, click through the UI via UI Automation |

The app accepts a few switches for testing: `--theme dark|light`, `--page search|history|favorites|lists|kanji|settings|about`,
`--search <text>`, `--radicals a,b`, `--tab overview|forms|kanji`, `--word-test click:<word>|select:<word>`.

## Credits

Dictionary words and kanji: JMdict, EDICT and KANJIDIC (Electronic Dictionary Research and Development Group, www.edrdg.org).
Stroke order: KanjiVG (Ulrich Apel). Example sentences: Tatoeba and the Tanaka Corpus. These come with the original
Takoboto data and are used under their own licences.

Pronunciation: speech synthesis by VOICEVOX CORE (voicevox.hiroshiba.jp), voices "VOICEVOX Nemo" (terms: voicevox.hiroshiba.jp/nemo/term/), "VOICEVOX:青山龍星", "VOICEVOX:玄野武宏" and "VOICEVOX:ずんだもん" (terms of the voice libraries: virvoxproject.com/voicevoxの利用規約, zunko.jp/con_ongen_kiyaku.html; the credit of the voice is required wherever the sound is used), ONNX Runtime built by the VOICEVOX project, OpenJTalk dictionary (NAIST Japanese Dictionary, BSD licence).

---

## По-русски

Неофициальная **портативная версия** японского словаря Takoboto: обычная программа для Windows, одна папка,
без установки и без приложения из Microsoft Store. Новый интерфейс и собственный поиск поверх одной базы SQLite,
собранной из открытых данных (JMdict, KANJIDIC, KanjiVG, Tatoeba): словарь - один файл `data\takoboto.db`, в него можно добавлять другие словари.

Что умеет: быстрый поиск (японский, английский, французский, немецкий, русский), карточка слова во вкладках
(Overview / Forms / Kanji), поиск кандзи по радикалам, рукописный ввод, история, избранное и свои списки, светлая / тёмная тема
(переключается сразу), настройки хранятся в папке `userdata` рядом с программой. Поиск работает по мере набора
(`nih` сразу показывает слова на に…, `食b` находит 食べ…), список подгружается при прокрутке. Текст в карточке можно
выделять; клик по слову в примере (или выделение слова) показывает маленькую подсказку с чтением и значением,
клик по подсказке открывает статью. Значок динамика у чтения и после каждого японского примера читает вслух без интернета (синтез речи VOICEVOX CORE); файлы голоса - папка `voice` рядом с программой (около 200 МБ), без неё программа работает, только без кнопок-динамиков.

**Что в репозитории:** только исходники программы (`portable-src/`), рисунки (`portable-src/Assets`) и вспомогательные скрипты (`tools/`).
Сам словарь `data\takoboto.db` и файлы произношения в репозиторий не входят. Готового скачиваемого релиза пока нет: сначала нужно собрать
программу самому (команда выше). Содержимое словаря - не работа автора приложения: это JMdict / EDICT / KANJIDIC (EDRDG), KanjiVG и
Tatoeba под открытыми лицензиями Creative Commons; они указаны в разделе Credits и на странице About программы. Маскот взят из оригинального Takoboto.
