using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AppleClock
{
    internal static class AnimationVerification
    {
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle, int command);
        static Dial FirstDial(DependencyObject root)
        {
            var dial = root as Dial; if (dial != null) return dial;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var found = FirstDial(VisualTreeHelper.GetChild(root, i)); if (found != null) return found; }
            return null;
        }
        static void Capture(Window window, string path)
        {
            window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var output = File.Create(path)) encoder.Save(output);
        }
        internal static void Run(ClockWindow window)
        {
            ShowWindow(new System.Windows.Interop.WindowInteropHelper(window).Handle, 5); window.Activate();
            var report = new StringBuilder();
            Action<bool, string> check = delegate(bool ok, string name) { report.AppendLine((ok ? "PASS " : "FAIL ") + name); if (!ok) Verification.Failed = true; };
            DateTime now = DateTime.UtcNow;
            var model = new Countdown { Duration = 60, Remaining = 60 }; model.Start(now);
            check(Dial.ProgressAt(model, now.AddMilliseconds(16)) < Dial.ProgressAt(model, now.AddMilliseconds(8)) && model.Remaining == 60, "fractional progress changes between 60 ms logic ticks without mutating countdown");
            model.Pause(now.AddMilliseconds(25)); double pausedProgress = Dial.ProgressAt(model, now);
            check(Dial.ProgressAt(model, now.AddMinutes(5)) == pausedProgress, "paused progress is stable");
            model.Reset(); check(Dial.ProgressAt(model, now) == 1, "reset restores a complete ring");
            model.Start(now); check(Dial.ProgressAt(model, now.AddMinutes(2)) == 0, "expired progress is clamped at zero");
            var timer = window.Data.Timers[0]; timer.Duration = timer.Remaining = 60; timer.Start(DateTime.UtcNow);
            window.Data.Dark = true; window.Page = 0; window.Build(); window.UpdateLayout();
            Dial dial = FirstDial(window), oldDial = null; long frames = dial.RenderCount, positions = dial.ProgressChangeCount;
            var elapsed = Stopwatch.StartNew(); int stage = 0;
            var probe = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            probe.Tick += delegate {
                try {
                    if (stage == 0 || stage == 1) {
                        long count = dial.RenderCount - frames;
                        report.AppendLine("MEASURE " + (stage == 0 ? "dark" : "light") + " ring: " + (count / elapsed.Elapsed.TotalSeconds).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " rendered FPS");
                        report.AppendLine("MEASURE distinct ring positions: " + ((dial.ProgressChangeCount - positions) / elapsed.Elapsed.TotalSeconds).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " per second");
                        check(dial.IsAnimating && count >= 2, "visible running ring renders repeatedly in " + (stage == 0 ? "dark" : "light") + " theme");
                    }
                    if (stage == 0) {
                        Directory.CreateDirectory("docs"); Capture(window, "docs/preview-animation-dark.png"); oldDial = dial;
                        DateTime deadline = timer.DeadlineUtc; window.Data.Dark = false; window.Build(); window.UpdateLayout();
                        window.Dispatcher.Invoke(new Action(delegate { }), DispatcherPriority.ApplicationIdle);
                        check(!oldDial.IsAnimating && timer.DeadlineUtc == deadline, "theme rebuild detaches old ring and preserves deadline");
                        dial = FirstDial(window); frames = dial.RenderCount; positions = dial.ProgressChangeCount; elapsed.Restart();
                    } else if (stage == 1) {
                        Capture(window, "docs/preview-animation-light.png"); timer.Pause(DateTime.UtcNow); window.Tick();
                        check(!dial.IsAnimating, "pause unsubscribes from frame rendering"); frames = dial.RenderCount;
                    } else if (stage == 2) {
                        check(dial.RenderCount - frames <= 1, "paused ring does not repaint on every logic tick");
                        timer.Start(DateTime.UtcNow); window.Tick(); check(dial.IsAnimating, "resume reattaches frame rendering");
                        oldDial = dial; window.Page = 4; window.Build(); window.Data.Focus.Start(DateTime.UtcNow); window.Tick(); window.UpdateLayout();
                        window.Dispatcher.Invoke(new Action(delegate { }), DispatcherPriority.ApplicationIdle);
                        dial = FirstDial(window); frames = dial.RenderCount; elapsed.Restart();
                        check(!oldDial.IsAnimating && timer.Running && dial.IsAnimating, "navigation detaches hidden timer ring and animates focus ring");
                    } else {
                        check(dial.RenderCount - frames >= 2, "focus countdown uses frame rendering too");
                        probe.Stop(); window.Close(); check(!dial.IsAnimating, "closing window detaches frame callback");
                        Directory.CreateDirectory("test-results"); File.WriteAllText("test-results/animation.txt", report.ToString());
                    }
                    stage++;
                } catch (Exception ex) {
                    check(false, ex.ToString()); probe.Stop(); Directory.CreateDirectory("test-results"); File.WriteAllText("test-results/animation.txt", report.ToString()); window.Close();
                }
            };
            probe.Start();
        }
    }
}
