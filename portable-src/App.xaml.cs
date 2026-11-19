using System;
using System.IO;
using System.Windows;

namespace TakobotoApp
{
    public partial class App : Application
    {
        public static string AppDir { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppDir = AppDomain.CurrentDomain.BaseDirectory;
            DispatcherUnhandledException += (s, ev) =>
            {
                Log(ev.Exception);
                MessageBox.Show("Something went wrong:\n\n" + ev.Exception.Message, "Takoboto", MessageBoxButton.OK, MessageBoxImage.Warning);
                ev.Handled = true;
            };

            try
            {
                Store.Load(AppDir);
                Theme.Apply(Store.Settings.Theme);
                Theme.WatchSystem();
                Engine.Init(Path.Combine(AppDir, "data"));
            }
            catch (Exception ex)
            {
                Log(ex);
                MessageBox.Show("Takoboto could not start.\n\n" + ex.Message +
                                "\n\nThe dictionary file 'takoboto.dat' must be in the 'data' folder next to Takoboto.exe.",
                                "Takoboto", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            var win = new MainWindow();
            win.Show();
            win.ApplyArgs(e.Args);
        }

        public static void Log(Exception ex)
        {
            try { File.AppendAllText(Path.Combine(Store.Dir ?? AppDir, "error.log"), DateTime.Now.ToString("s") + "  " + ex + "\r\n\r\n"); }
            catch { }
        }
    }
}
