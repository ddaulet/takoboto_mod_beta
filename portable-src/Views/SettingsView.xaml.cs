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
            VoiceFemale.IsChecked = !s.VoiceMale;
            VoiceMale.IsChecked = s.VoiceMale;
            if (Voice.Available)
                VoiceInfo.Text = "The loudspeaker next to a reading and after each example sentence reads it aloud. Works offline; the voice is VOICEVOX Nemo.";
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

        void Voice_Checked(object sender, RoutedEventArgs e)
        {
            if (loading) return;
            Store.Settings.VoiceMale = VoiceMale.IsChecked == true;
            Store.SaveSettings();
            Voice.Speak(Sample, Store.Settings.VoiceMale, null);   // the new voice says hello
        }

        void VoiceTest_Click(object sender, RoutedEventArgs e)
        {
            Voice.Speak(Sample, Store.Settings.VoiceMale, null);
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
