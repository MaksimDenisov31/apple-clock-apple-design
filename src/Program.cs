using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

[assembly: AssemblyTitle("Apple Clock — Apple Design")]
[assembly: AssemblyProduct("Apple Clock")]
[assembly: AssemblyVersion("1.0.3.0")]
namespace AppleClock
{
    public static class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr handle);
        [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr handle, int command);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr handle);
        internal static void ActivateExisting(string title = "Apple Clock")
        {
            for (int i = 0; i < 20; i++)
            {
                IntPtr handle = FindWindow(null, title);
                if (handle != IntPtr.Zero) { ShowWindowAsync(handle, IsIconic(handle) ? 9 : 5); SetForegroundWindow(handle); return; }
                System.Threading.Thread.Sleep(100);
            }
        }
        [STAThread] public static int Main(string[] args)
        {
            bool test = args.Length > 0 && (args[0] == "--self-test" || args[0] == "--ui-test" || args[0] == "--alert-test");
            try
            {
                if (test) Storage.DirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results", Guid.NewGuid().ToString("N"));
                if (args.Length > 0 && args[0] == "--self-test") return Verification.Core();
                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) { MessageBox.Show(e.Exception.Message, "Apple Clock", MessageBoxButton.OK, MessageBoxImage.Error); e.Handled = true; };
                // A repeated launch brings the existing window forward.
                bool created;
                using (var mutex = new System.Threading.Mutex(true, test ? "AppleClock.Test." + Guid.NewGuid() : "Local\\AppleClock.MaksimDenisovRussia", out created))
                {
                    if (!created) { ActivateExisting(); return 0; }
                    var window = new ClockWindow(test ? State.Fresh() : Storage.Load(), test);
                    if (test) window.Loaded += delegate { window.Dispatcher.BeginInvoke(new Action(delegate { if (args[0] == "--alert-test") Verification.AlertDuration(window); else Verification.UI(window); })); };
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


