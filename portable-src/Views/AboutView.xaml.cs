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
        }
    }
}
