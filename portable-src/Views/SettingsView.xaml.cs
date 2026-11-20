using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace TakobotoApp
{
    public partial class SettingsView : UserControl
    {
        bool loading;

        public SettingsView()
        {
            InitializeComponent();
            loading = true;
            var s = Store.Settings;
            ThemeSystem.IsChecked = s.Theme == "system";
            ThemeLight.IsChecked = s.Theme == "light";
            ThemeDark.IsChecked = s.Theme != "system" && s.Theme != "light";
            CheckEnglish.IsChecked = s.English;
            CheckFrench.IsChecked = s.French;
            CheckGerman.IsChecked = s.German;
            CheckRussian.IsChecked = s.Russian;
            CheckFurigana.IsChecked = s.Furigana;
            var chosen = Voice.Find(s.VoiceKey, s.VoiceMale);
            // one row per voice: the choice, and a loudspeaker to the right of it to listen (nothing is played by choosing)
            foreach (var v in Voice.Installed())
            {
                var voice = v;
                AddChoiceRow(VoiceList, voice.Name, "voice", chosen != null && voice.Key == chosen.Key, voice, Voice_Checked,
                    Ui.SpeakButton(() => Sample, 30, () => voice.Key, () => Store.Settings.VoiceSpeed));
            }
            // the same for the speeds; they are heard in the voice that is chosen above
            string[] speedNames = { "Fast", "Medium", "Slow" };
            for (int i = 0; i < 3; i++)
            {
                int level = i;
                AddChoiceRow(SpeedList, speedNames[i], "voicespeed", Math.Min(2, Math.Max(0, s.VoiceSpeed)) == level, level, Speed_Checked,
                    Ui.SpeakButton(() => Sample, 30, () => Store.Settings.VoiceKey, () => level));
            }
            if (Voice.Available)
                VoiceInfo.Text = "The loudspeaker next to a reading and after each example sentence reads it aloud. Works offline; the voices are from VOICEVOX.";
            else
            {
                VoiceInfo.Text = "The voice files are not found. Put the folder \"voice\" next to Takoboto.exe (it comes with the full download) and restart the program: the loudspeaker buttons then appear.";
                VoicePanel.Visibility = Visibility.Collapsed;
            }
            CheckRandomCommon.IsChecked = s.RandomCommon;
            CheckRandomKatakana.IsChecked = s.RandomSkipKatakana;
            DataPath.Text = Store.Dir;

            // one round colour dot + name per scheme
            foreach (var sc in Schemes.All)
            {
                var scheme = sc;
                var rb = new RadioButton
                {
                    Style = (Style)FindResource("SwatchRadio"), Content = scheme.Name, GroupName = "scheme", Margin = new Thickness(0, 0, 12, 10),
                    Tag = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(scheme.Swatch)),
                    IsChecked = Schemes.Current.Id == scheme.Id,
                };
                AutomationProperties.SetName(rb, "Colour scheme " + scheme.Name);
                rb.Checked += (a, b) => { if (!loading) Schemes.Set(scheme.Id); };
                SchemePanel.Children.Add(rb);
            }
            loading = false;
        }

        void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (loading) return;
            string mode = ThemeLight.IsChecked == true ? "light" : ThemeDark.IsChecked == true ? "dark" : "system";
            Store.Settings.Theme = mode;
            Store.SaveSettings();
            Theme.Apply(mode);
        }

        void Language_Click(object sender, RoutedEventArgs e)
        {
            if (loading) return;
            var s = Store.Settings;
            // at least one language has to stay ticked
            if (CheckEnglish.IsChecked != true && CheckFrench.IsChecked != true && CheckGerman.IsChecked != true && CheckRussian.IsChecked != true)
            {
                loading = true; ((CheckBox)sender).IsChecked = true; loading = false;
            }
            s.English = CheckEnglish.IsChecked == true;
            s.French = CheckFrench.IsChecked == true;
            s.German = CheckGerman.IsChecked == true;
            s.Russian = CheckRussian.IsChecked == true;
            Store.SaveSettings();
            MainWindow.Instance.RefreshSearch();
        }

        const string Sample = "こんにちは。タコボトへようこそ。";

        void AddChoiceRow(Panel list, string text, string group, bool isChecked, object tag, RoutedEventHandler onChecked, Button speak)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6), HorizontalAlignment = HorizontalAlignment.Left };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(225) });   // the loudspeakers stand in one column
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var rb = new RadioButton { Style = (Style)FindResource("ChoiceRadio"), GroupName = group, Tag = tag, Content = text, VerticalAlignment = VerticalAlignment.Center, IsChecked = isChecked };
            rb.Checked += onChecked;
            row.Children.Add(rb);
            speak.Margin = new Thickness(0);
            speak.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(speak, 1);
            row.Children.Add(speak);
            list.Children.Add(row);
        }

        void Voice_Checked(object sender, RoutedEventArgs e)
        {
            if (loading) return;
            var voice = ((RadioButton)sender).Tag as Voice.VoiceInfo;
            if (voice == null) return;
            Store.Settings.VoiceKey = voice.Key;
            Store.SaveSettings();
        }

        void Speed_Checked(object sender, RoutedEventArgs e)
        {
            if (loading) return;
            Store.Settings.VoiceSpeed = (int)((RadioButton)sender).Tag;
            Store.SaveSettings();
        }

        void Furigana_Click(object sender, RoutedEventArgs e)
        {
            if (loading) return;
            Store.Settings.Furigana = CheckFurigana.IsChecked == true;
            Store.SaveSettings();
            MainWindow.Instance.RefreshCard();   // the open word card is built again with / without furigana
        }

        void Random_Click(object sender, RoutedEventArgs e)
        {
            if (loading) return;
            Store.Settings.RandomCommon = CheckRandomCommon.IsChecked == true;
            Store.Settings.RandomSkipKatakana = CheckRandomKatakana.IsChecked == true;
            Store.SaveSettings();
        }

        void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start("explorer.exe", "\"" + Store.Dir + "\""); } catch { }
        }
    }
}
