using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TakobotoApp
{
    // The objects of the dictionary, as the screens use them. They are made from our own database (Data\Db.cs).

    public enum Language { Japanese, English, French, German, Russian, Chinese }
    public enum EntryType { Kanji = 1, Kana, Romaji, Sense }

    public enum WordType : byte
    {
        NoType, CopulaPlain, CopulaPolite, IAdjective, VerbIchidan, VerbKu, VerbGu, VerbSu, VerbTsu, VerbNu, VerbBu, VerbMu, VerbRu, VerbU,
        VerbIku, VerbKuru, VerbSuru, VerbAru, VerbIrassharuAndOthers, VerbGozaru
    }

    public class WordInfo
    {
        public string Code { get; set; }
        public string Text { get; set; }
    }

    public class WordPart
    {
        public int WordId { get; set; }
        public string Text { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsSecondary { get { return !IsPrimary; } }
        public IList<WordInfo> Infos { get; set; }
        public Language Language { get; set; }
        public EntryType EntryType { get; set; }
        public WordType WordType { get; set; }
        public bool IsKanaWord { get; set; }
        public bool HasInfo { get { return Infos != null && Infos.Count > 0; } }
        /// <summary>An irregular / obsolete spelling (the dictionary marks them with these codes).</summary>
        public bool HasInfoMarkedSecondary
        {
            get
            {
                if (Infos == null) return false;
                foreach (var i in Infos)
                    if (i.Code == "#iK" || i.Code == "#ik" || i.Code == "#io" || i.Code == "#oK" || i.Code == "#obs" || i.Code == "#ok" || i.Code == "#oik") return true;
                return false;
            }
        }
    }

    public class WordMeaning
    {
        public IList<WordPart> Parts { get; set; }
        public IList<WordInfo> Infos { get; set; }
        public bool HasInfos { get { return Infos != null && Infos.Count > 0; } }
        public WordMeaning() { Parts = new List<WordPart>(); Infos = new List<WordInfo>(); }
    }

    public class KanjiMeaning
    {
        public Language Language { get; set; }
        public string Text { get; set; }
    }

    public class StrokePath { public string Data; public bool IsLast; }
    public class KanjiStrokes { public List<StrokePath> Paths = new List<StrokePath>(); }

    public class WordKanji
    {
        public double Opacity = 1;
        public ushort OrderValue;
        public string Kanji;
        public string OnReading, KunReading;
        public IList<KanjiMeaning> Meanings = new List<KanjiMeaning>();
        public bool IsSecondary;
        public bool HasOnReading { get { return !string.IsNullOrEmpty(OnReading); } }
        public bool HasKunReading { get { return !string.IsNullOrEmpty(KunReading); } }
        public IList<KanjiStrokes> KanjiStrokes = new List<KanjiStrokes>();
        public string AllMeanings { get { return string.Join(", ", Meanings.Select(m => m.Text)); } }
    }

    public class SentenceTranslation
    {
        public Language Language { get; set; }
        public string Text { get; set; }
        public SentenceTranslation(Language language, string text) { Language = language; Text = text; }
    }

    public class SentencePart
    {
        public int Offset { get; set; }
        public string Writing { get; set; }
        public string Reading { get; set; }
        public bool HasReading { get { return !string.IsNullOrEmpty(Reading); } }
        public string Text { get { return Writing; } }
        public SentencePart(string writing, string reading, int offset) { Writing = writing; Reading = reading; Offset = offset; }
    }

    public class Sentence
    {
        public int Id;
        public string MainText { get; set; }
        public IList<SentenceTranslation> Translations { get; set; }
        public IList<SentencePart> Parts { get; set; }
        public Sentence(string mainText) { MainText = mainText; Translations = new List<SentenceTranslation>(); Parts = new List<SentencePart>(); }
    }

    public class WordForm
    {
        public Word Word { get; private set; }
        public string Plain { get; set; }
        public string Polite { get; set; }
        public string Explanation { get; set; }
        public string AdditionalPlain { get; set; }
        public string AdditionalExplanation { get; set; }
        public bool HasPolite { get { return !string.IsNullOrEmpty(Polite); } }
        public bool HasAdditionalPlain { get { return !string.IsNullOrEmpty(AdditionalPlain); } }
        public bool HasExplanation { get { return !string.IsNullOrEmpty(Explanation); } }
        public bool HasAdditionalExplanation { get { return !string.IsNullOrEmpty(AdditionalExplanation); } }
        public WordForm(Word word, string plain, string polite, string explanation) { Word = word; Plain = plain; Polite = polite; Explanation = explanation; }
    }

    /// <summary>The conjugations of a verb or adjective; the fields are shown in this order (the screen reads them with reflection).</summary>
    public class WordForms
    {
        public WordForm Nonpast, NonpastNegative, Past, PastNegative, TeForm, TeFormNegative, TeFormNegative2, Progressive, ProgressiveNegative,
            Volitional, VolitionalNegative, Imperative, ImperativeNegative, Request, RequestNegative, Provisional, ProvisionalNegative,
            Conditional, ConditionalNegative, Potential, PotentialNegative, Passive, PassiveNegative, Causative, CausativeNegative,
            CausativePassive, CausativePassiveNegative, Adverbial, Noun;
    }

    /// <summary>A word as a result row: the writings and a short meaning.</summary>
    public class WordTag
    {
        public bool IsExactMatch { get; set; }
        public int WordId { get; set; }
        public IList<WordPart> JapaneseParts { get; set; }
        public IList<WordPart> TranslationParts { get; set; }
        public bool Conjugated { get; set; }
        public string ConjugatedText { get; set; }
        public string HighlightText { get; set; }
        public WordTag(int wordId, bool isExactMatch, string highlightText) { WordId = wordId; IsExactMatch = isExactMatch; HighlightText = highlightText; }
    }

    public class Word
    {
        public int WordId { get; set; }
        public IList<WordPart> MainEntries { get; set; }
        public IList<WordPart> Readings { get; set; }
        public IList<WordMeaning> Meanings { get; set; }
        public IList<WordKanji> Kanjis { get; set; }
        public IList<Sentence> RecommendedSentences { get; set; }
        public WordForms Forms { get; set; }
        public bool HasReadings { get { return Readings != null && Readings.Count > 0; } }
        public bool HasKanjis { get { return Kanjis != null && Kanjis.Count > 0; } }
        public bool HasForms { get { return Forms != null; } }
        public Word(int wordId) { WordId = wordId; MainEntries = new List<WordPart>(); Readings = new List<WordPart>(); Meanings = new List<WordMeaning>(); Kanjis = new List<WordKanji>(); RecommendedSentences = new List<Sentence>(); }
    }
}
