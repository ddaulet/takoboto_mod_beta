using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TakobotoApp
{
    /// <summary>The on-screen keyboard of the search page: ABC (English letters), hiragana and katakana. A strip on top
    /// switches the layout - the layout also decides what the REAL keyboard types (in the kana layouts romaji becomes kana)
    /// - and folds the keys away. The keys never take the focus, so the caret stays in the search box.</summary>
    public class InputKeyboard : UserControl
    {
        public event Action<string> TextTyped;     // insert this text
        public event Action<string> Modify;        // "dakuten" | "handakuten" | "small": change the last kana
        public event Action BackspaceTyped;
        public event Action LayoutChanged;

        readonly ContentControl keysHost = new ContentControl();
        readonly TextBlock hint;
        readonly Button fold;
        readonly Dictionary<string, RadioButton> chips = new Dictionary<string, RadioButton>();
        string layout = "abc";
        bool keysVisible = true, building;

        // the 50-sound table, columns = a ka sa ta na ha ma ya ra wa, rows = a i u e o ("" = no key)
        static readonly string[] KanaRows =
        {
            "あかさたなはまやらわ",
            "いきしちにひみ　り　",
            "うくすつぬふむゆるん",
            "えけせてねへめ　れー",
            "おこそとのほもよろを",
        };

        public InputKeyboard()
        {
            var root = new StackPanel();

            // strip: layout choice | hint | fold button
            var strip = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            strip.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            strip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            strip.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var chipRow = new StackPanel { Orientation = Orientation.Horizontal };
            AddChip(chipRow, "abc", "ABC", "English letters");
            AddChip(chipRow, "hira", "あ", "Hiragana: the on-screen keys and your own keyboard (type romaji: ka → か)");
            AddChip(chipRow, "kata", "ア", "Katakana: the on-screen keys and your own keyboard (type romaji: ka → カ)");
            strip.Children.Add(chipRow);
            hint = new TextBlock { FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "FgFaint");
            Grid.SetColumn(hint, 1);
            strip.Children.Add(hint);
            fold = new Button { Style = (Style)Application.Current.FindResource("IconButton"), FontSize = 12, Focusable = false };
            fold.Click += (s, e) => KeysVisible = !KeysVisible;
            Grid.SetColumn(fold, 2);
            strip.Children.Add(fold);

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

        public string Layout
        {
            get { return layout; }
            set
            {
                if (value != "hira" && value != "kata") value = "abc";
                layout = value;
                Rebuild();
            }
        }

        public event Action KeysToggled;

        public bool KeysVisible
        {
            get { return keysVisible; }
            set
            {
                bool changed = keysVisible != value;
                keysVisible = value;
                Rebuild();
                if (changed && KeysToggled != null) KeysToggled();
            }
        }

        void Rebuild()
        {
            building = true;
            foreach (var kv in chips) kv.Value.IsChecked = kv.Key == layout;
            building = false;
            hint.Text = layout == "abc" ? "" : "your keyboard: romaji → kana";
            fold.Content = keysVisible ? "" : "";   // chevron up / down
            fold.ToolTip = keysVisible ? "Hide the keys" : "Show the keys";
            keysHost.Content = keysVisible ? (layout == "abc" ? BuildAbc() : BuildKana(layout == "kata")) : null;
        }

        // ---- layouts -------------------------------------------------------------------------------------------------
        class Key { public string Text; public double Weight = 1; public Action Click; public bool Blank; public double FontSize; }

        Key Letter(string t) { return new Key { Text = t, Click = () => Type(t) }; }

        void Type(string t) { if (TextTyped != null) TextTyped(t); }

        Key BackspaceKey(double weight)
        {
            return new Key { Text = "⌫", Weight = weight, Click = () => { if (BackspaceTyped != null) BackspaceTyped(); } };
        }

        UIElement BuildAbc()
        {
            var rows = new List<List<Key>>();
            rows.Add("1234567890".Select(c => Letter(c.ToString())).ToList());
            rows.Add("qwertyuiop".Select(c => Letter(c.ToString())).ToList());
            var r3 = "asdfghjkl".Select(c => Letter(c.ToString())).ToList(); r3.Add(BackspaceKey(1)); rows.Add(r3);
            rows.Add("zxcvbnm-'.".Select(c => Letter(c.ToString())).ToList());
            rows.Add(new List<Key> { new Key { Text = "space", Weight = 10, Click = () => Type(" ") } });
            return KeyRows(rows);
        }

        UIElement BuildKana(bool katakana)
        {
            Func<string, string> show = s => katakana ? Kana.ToKatakana(s) : s;
            var rows = new List<List<Key>>();
            foreach (var line in KanaRows)
            {
                var r = new List<Key>();
                foreach (char c in line)
                {
                    if (c == '　') { r.Add(new Key { Blank = true }); continue; }
                    string t = show(c.ToString());
                    r.Add(new Key { Text = t, Click = () => Type(t) });
                }
                rows.Add(r);
            }
            // modifiers: ゛ ゜ small ー, then backspace and space
            rows.Add(new List<Key>
            {
                new Key { Text = "゛", FontSize = 26, Click = () => { if (Modify != null) Modify("dakuten"); } },
                new Key { Text = "゜", FontSize = 26, Click = () => { if (Modify != null) Modify("handakuten"); } },
                new Key { Text = katakana ? "ァ" : "ぁ", Click = () => { if (Modify != null) Modify("small"); } },
                new Key { Text = "、", Click = () => Type("、") },
                new Key { Text = "。", Click = () => Type("。") },
                BackspaceKey(2),
                new Key { Text = "space", Weight = 3, Click = () => Type(" ") },
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
