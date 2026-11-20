using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TakobotoApp
{
    /// <summary>The on-screen kana keyboard of the search page (hiragana or katakana). It is shown while the keyboard mode is
    /// selected; in that mode the REAL keyboard types romaji and kana come out (see SearchView). The keys never take the
    /// focus, so the caret stays in the search box.</summary>
    public class InputKeyboard : UserControl
    {
        public event Action<string> TextTyped;     // insert this text
        public event Action<string> Modify;        // "dakuten" | "handakuten": change the last kana
        public event Action BackspaceTyped;
        public event Action LayoutChanged;

        readonly ContentControl keysHost = new ContentControl();
        readonly Dictionary<string, RadioButton> chips = new Dictionary<string, RadioButton>();
        string layout = "hira";
        bool building;

        // the 50-sound table, columns = a ka sa ta na ha ma ya ra wa, rows = a i u e o ("　" = no key)
        static readonly string[] KanaRows =
        {
            "あかさたなはまやらわ",
            "いきしちにひみ　り　",
            "うくすつぬふむゆるん",
            "えけせてねへめ　れー",
            "おこそとのほもよろを",
        };

        // the small kana, two columns next to the table: ぁ ゃ / ぃ ゅ / ぅ ょ / ぇ っ / ぉ ゎ  (っ doubles the next consonant)
        static readonly string[] SmallRows = { "ぁゃ", "ぃゅ", "ぅょ", "ぇっ", "ぉゎ" };

        public InputKeyboard()
        {
            var root = new StackPanel();

            // strip: hiragana / katakana, and a reminder that the real keyboard works too
            var strip = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            strip.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            strip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var chipRow = new StackPanel { Orientation = Orientation.Horizontal };
            AddChip(chipRow, "hira", "あ", "Hiragana: the on-screen keys and your own keyboard (type romaji: ka → か)");
            AddChip(chipRow, "kata", "ア", "Katakana: the on-screen keys and your own keyboard (type romaji: ka → カ)");
            strip.Children.Add(chipRow);
            var hint = new TextBlock { Text = "your keyboard too: romaji → kana", FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "FgFaint");
            Grid.SetColumn(hint, 1);
            strip.Children.Add(hint);

            root.Children.Add(strip);
            root.Children.Add(keysHost);
            Content = root;
            Rebuild();
        }

        void AddChip(Panel row, string id, string text, string tip)
        {
            var rb = new RadioButton { Style = (Style)Application.Current.FindResource("SegmentRadio"), Content = text, GroupName = "kbd-layout", ToolTip = tip, Focusable = false };
            System.Windows.Automation.AutomationProperties.SetName(rb, "Keyboard layout " + text);
            rb.Checked += (s, e) => { if (!building) { Layout = id; if (LayoutChanged != null) LayoutChanged(); } };
            chips[id] = rb;
            row.Children.Add(rb);
        }

        /// <summary>"hira" or "kata".</summary>
        public string Layout
        {
            get { return layout; }
            set
            {
                layout = value == "kata" ? "kata" : "hira";
                Rebuild();
            }
        }

        public bool Katakana { get { return layout == "kata"; } }

        void Rebuild()
        {
            building = true;
            foreach (var kv in chips) kv.Value.IsChecked = kv.Key == layout;
            building = false;
            keysHost.Content = BuildKana(layout == "kata");
        }

        // ---- keys ----------------------------------------------------------------------------------------------------
        class Key { public string Text; public double Weight = 1; public Action Click; public bool Blank; public double FontSize; }

        void Type(string t) { if (TextTyped != null) TextTyped(t); }

        UIElement BuildKana(bool katakana)
        {
            Func<string, string> show = s => katakana ? Kana.ToKatakana(s) : s;
            Func<string, Key> kana = c => { string t = show(c); return new Key { Text = t, Click = () => Type(t) }; };

            var rows = new List<List<Key>>();
            for (int r = 0; r < KanaRows.Length; r++)
            {
                var row = new List<Key>();
                foreach (char c in KanaRows[r]) row.Add(c == '　' ? new Key { Blank = true } : kana(c.ToString()));
                row.Add(new Key { Blank = true, Weight = 0.4 });                       // a gap, then the small kana
                foreach (char c in SmallRows[r]) row.Add(kana(c.ToString()));
                rows.Add(row);
            }
            // voicing marks, punctuation, backspace, space
            rows.Add(new List<Key>
            {
                new Key { Text = "゛", FontSize = 26, Click = () => { if (Modify != null) Modify("dakuten"); } },
                new Key { Text = "゜", FontSize = 26, Click = () => { if (Modify != null) Modify("handakuten"); } },
                new Key { Text = "、", Click = () => Type("、") },
                new Key { Text = "。", Click = () => Type("。") },
                new Key { Text = "⌫", Weight = 3, Click = () => { if (BackspaceTyped != null) BackspaceTyped(); } },
                new Key { Text = "space", Weight = 5.4, Click = () => Type(" ") },
            });
            return KeyRows(rows);
        }

        UIElement KeyRows(List<List<Key>> rows)
        {
            var panel = new StackPanel();
            foreach (var row in rows)
            {
                var g = new Grid();
                foreach (var k in row) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(k.Weight, GridUnitType.Star) });
                for (int i = 0; i < row.Count; i++)
                {
                    var key = row[i];
                    if (key.Blank) continue;
                    var b = new Button { Style = (Style)Application.Current.FindResource("KeyButton"), Content = key.Text, Focusable = false, Margin = new Thickness(2) };
                    var click = key.Click;
                    if (key.FontSize > 0) b.FontSize = key.FontSize;
                    b.Click += (s, e) => click();
                    Grid.SetColumn(b, i);
                    g.Children.Add(b);
                }
                panel.Children.Add(g);
            }
            return panel;
        }
    }
}
