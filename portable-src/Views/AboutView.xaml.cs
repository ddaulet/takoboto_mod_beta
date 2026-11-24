using System.Reflection;
using System.Windows.Controls;

namespace TakobotoApp
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            InitializeComponent();
            Logo.Source = Ui.Resource("Logo.png");
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = "Portable edition " + v.Major + "." + v.Minor + "." + v.Build;
            VoiceCredit.Text = "Pronunciation (the loudspeaker buttons): speech synthesis by VOICEVOX CORE (voicevox.hiroshiba.jp). Voices: VOICEVOX Nemo; VOICEVOX:青山龍星; VOICEVOX:玄野武宏; VOICEVOX:ずんだもん. Works offline.";
            Footer.Text = "This is a portable desktop edition: one folder, no installation, one SQLite database. Keyboard: Ctrl+F or Ctrl+1 search, Ctrl+2 history, Ctrl+3 favorites, Ctrl+4 lists, Ctrl+5 kanji by radicals, Ctrl+6 grammar. Size of the interface: Ctrl + mouse wheel, Ctrl + / Ctrl -, Ctrl 0.";
        }
    }
}
