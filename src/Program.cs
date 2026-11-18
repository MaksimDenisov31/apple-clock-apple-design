using System;
using System.IO;
using System.Reflection;
using System.Windows;

[assembly: AssemblyTitle("Apple Clock — Apple Design")]
[assembly: AssemblyProduct("Apple Clock")]
[assembly: AssemblyVersion("1.0.0.0")]
namespace AppleClock
{
    public static class Program
    {
        [STAThread] public static int Main(string[] args)
        {
            bool test = args.Length > 0 && (args[0] == "--self-test" || args[0] == "--ui-test");
            try
            {
                if (test) Storage.DirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results", Guid.NewGuid().ToString("N"));
                if (args.Length > 0 && args[0] == "--self-test") return Verification.Core();
                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) { MessageBox.Show(e.Exception.Message, "Apple Clock", MessageBoxButton.OK, MessageBoxImage.Error); e.Handled = true; };
                // Avoid two background instances firing the same alarm.
                bool created;
                using (var mutex = new System.Threading.Mutex(true, test ? "AppleClock.Test." + Guid.NewGuid() : "Local\\AppleClock.MaksimDenisovRussia", out created))
                {
                    if (!created) { MessageBox.Show("Часы уже запущены. Откройте их через значок в области уведомлений Windows.", "Apple Clock"); return 0; }
                    var window = new ClockWindow(test ? State.Fresh() : Storage.Load(), test);
                    if (test) window.Loaded += delegate { window.Dispatcher.BeginInvoke(new Action(delegate { Verification.UI(window); })); };
                    else if (Storage.LastError.Length > 0) window.Loaded += delegate { MessageBox.Show(window, Storage.LastError, "Apple Clock"); };
                    app.Run(window); return Verification.Failed ? 1 : 0;
                }
            }
            catch (Exception ex)
            {
                if (test) { Directory.CreateDirectory("test-results"); File.WriteAllText(Path.Combine("test-results", "fatal.txt"), ex.ToString()); }
                else MessageBox.Show(ex.Message, "Apple Clock", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }
    }
}
