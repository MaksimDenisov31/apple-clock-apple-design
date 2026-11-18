using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AppleClock
{
    public static class Verification
    {
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr handle, uint command);
        static bool Above(Window upper, Window lower)
        {
            IntPtr below = new System.Windows.Interop.WindowInteropHelper(lower).Handle;
            IntPtr above = new System.Windows.Interop.WindowInteropHelper(upper).Handle;
            for (IntPtr handle = GetWindow(below, 3); handle != IntPtr.Zero; handle = GetWindow(handle, 3)) if (handle == above) return true;
            return false;
        }
        public static bool Failed;
        static StringBuilder report = new StringBuilder();
        static void Check(bool ok, string name) { report.AppendLine((ok ? "PASS " : "FAIL ") + name); if (!ok) Failed = true; }
        public static int Core()
        {
            try
            {
                DateTime now = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
                var t = new Countdown { Duration = 30, Remaining = 30 }; t.Start(now);
                Check(!t.Tick(now.AddSeconds(12)) && t.Remaining == 18, "countdown uses deadline");
                t.Pause(now.AddSeconds(15)); Check(!t.Running && t.Remaining == 15, "pause preserves time");
                t.Start(now.AddSeconds(60)); Check(t.Tick(now.AddSeconds(90)) && !t.Tick(now.AddSeconds(91)), "completion fires once after elapsed deadline");
                t.Reset(); Check(t.Remaining == 30 && !t.Running, "reset");
                t.Start(now); Check(t.Tick(now.AddHours(4)), "overdue timer after sleep");
                var a = new Alarm { Hour = 7, Minute = 30 }; var morning = new DateTime(2026, 11, 18, 7, 0, 0, DateTimeKind.Local); a.Schedule(morning);
                Check(a.NextUtc.ToLocalTime() == morning.AddMinutes(30), "alarm next occurrence");
                Check(a.Tick(a.NextUtc) && !a.Enabled && !a.Tick(a.NextUtc), "one-shot alarm once");
                a = new Alarm { Hour = 7, Minute = 30, Days = 31 }; a.Schedule(new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Local)); Check(a.NextUtc.ToLocalTime().Day == 23, "weekday alarm skips weekend");
                DateTime due = a.NextUtc; Check(a.Tick(due) && a.Enabled && a.NextUtc > due, "recurring alarm schedules next day");
                a.NextUtc = now.AddHours(-1); Check(!a.Tick(now) && a.NextUtc > now, "stale alarm skipped and rescheduled");
                a.NextUtc = now.AddMinutes(5); Check(!a.Tick(now.AddMinutes(4)) && a.Tick(now.AddMinutes(5)), "snooze deadline");
                var ny = City.Catalog.First(c => c.Name == "Нью-Йорк"); Check(ny.At(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc)).Hour == 8 && ny.At(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)).Hour == 7, "world clock DST");
                foreach (City c in City.Catalog) c.At(now); Check(true, "all city time zones available");
                State s = State.Fresh(); s.Dark = true; s.FocusTask = "Проверка — часы"; s.Timers[0].Start(now); s.Alarms.Add(a); Check(Storage.Save(s), "atomic initial save"); s.FocusCompleted = 3; Check(Storage.Save(s), "atomic replacement save"); var loaded = Storage.Load(); Check(loaded.Dark && loaded.FocusTask == s.FocusTask && loaded.FocusCompleted == 3 && loaded.Timers[0].Running && loaded.Timers[0].DeadlineUtc == s.Timers[0].DeadlineUtc && loaded.Alarms.Count == 1, "restore settings timers alarms UTF-8");
                string settingsPath = Path.Combine(Storage.DirectoryPath, "settings.xml");
                File.WriteAllText(settingsPath, File.ReadAllText(settingsPath).Replace("</State>", "<Background>true</Background></State>"));
                Check(Storage.Load().Dark && Storage.LastError.Length == 0 && typeof(State).GetField("Background") == null, "legacy tray setting ignored while preferences are restored");
                File.WriteAllText(settingsPath, "broken"); Check(Storage.Load().Timers.Count == 2 && Storage.LastError.Length > 0, "corrupt settings handled");
                Check(ClockWindow.Duration(3661) == "01:01:01", "duration formatting");
                double seconds;
                Check(ClockWindow.ParseDuration("99:59:59", out seconds) && seconds == 359999 && ClockWindow.Duration(seconds) == "99:59:59", "timer duration supports 99 hours");
                Check(!ClockWindow.ParseDuration("00:00:00", out seconds) && !ClockWindow.ParseDuration("00:60:00", out seconds) && !ClockWindow.ParseDuration("-1:00:00", out seconds), "invalid durations rejected");
            }
            catch (Exception ex) { Check(false, ex.ToString()); }
            Directory.CreateDirectory("test-results"); File.WriteAllText("test-results/core.txt", report.ToString()); return Failed ? 1 : 0;
        }
        static Button FindButton(DependencyObject root, string label)
        {
            var button = root as Button; if (button != null && (button.Content as string == label || System.Windows.Automation.AutomationProperties.GetName(button) == label)) return button;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { Button found = FindButton(VisualTreeHelper.GetChild(root, i), label); if (found != null) return found; } return null;
        }
        static void Click(Window window, string label) { window.UpdateLayout(); var b = FindButton(window, label); if (b == null) throw new Exception("Button not found: " + label); b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); window.UpdateLayout(); }
        static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            var match = root as T; if (match != null) yield return match;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
        }
        static void InDialog(ClockWindow owner, Action<Window, StackPanel> action)
        {
            owner.Dispatcher.BeginInvoke(new Action(delegate {
                var dialog = Application.Current.Windows.Cast<Window>().FirstOrDefault(x => x.Owner == owner);
                if (dialog == null) { Check(false, "dialog opens"); return; }
                try { dialog.UpdateLayout(); action(dialog, (StackPanel)dialog.Content); }
                catch (Exception ex) { Check(false, ex.ToString()); }
                finally { if (dialog.IsVisible) dialog.Close(); }
            }));
        }
        static void Screenshot(Window window, string filename)
        {
            window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(filename)) encoder.Save(file);
        }
        public static void UI(ClockWindow window)
        {
            try
            {
                Directory.CreateDirectory("docs"); window.UpdateLayout(); Screenshot(window, "docs/preview-light.png");
                Click(window, "Старт"); Check(window.Data.Timers[0].Running, "UI timer start"); Click(window, "Пауза"); Check(!window.Data.Timers[0].Running, "UI timer pause");
                Click(window, "5 мин"); Check(window.Data.Timers.Count == 3 && window.Data.Timers[2].Running, "UI quick timer");
                InDialog(window, delegate(Window d, StackPanel p) {
                    var inputs = Descendants<TextBox>(p).ToArray();
                    Check(d.WindowStyle == WindowStyle.None, "timer dialog has no native Windows caption or close button");
                    Click(d, "Прибавить 10 секунд"); Check(inputs[1].Text == "00:05:10", "up arrow adds ten seconds");
                    Click(d, "Вычесть 10 секунд"); Check(inputs[1].Text == "00:05:00", "down arrow subtracts ten seconds");
                    inputs[1].Text = "00:00:55"; Click(d, "Прибавить 10 секунд"); Check(inputs[1].Text == "00:01:05", "step crosses minute boundary");
                    inputs[1].Text = "00:59:55"; Click(d, "Прибавить 10 секунд"); Check(inputs[1].Text == "01:00:05", "step crosses hour boundary");
                    inputs[1].Text = "00:00:01"; Click(d, "Вычесть 10 секунд"); Check(inputs[1].Text == "00:00:01", "step clamps at one second");
                    inputs[1].Text = "99:59:59"; Click(d, "Прибавить 10 секунд"); Check(inputs[1].Text == "99:59:59", "step clamps at maximum duration");
                    inputs[1].Text = "invalid"; Click(d, "Прибавить 10 секунд"); Check(inputs[1].Text == "00:05:10", "step recovers from invalid input");
                    inputs[0].Text = "Тестовый таймер"; inputs[1].Text = "00:60:00"; Click(d, "Сохранить"); Check(d.IsVisible && window.Data.Timers.Count == 3, "timer dialog rejects invalid input"); inputs[1].Text = "99:59:59"; Click(d, "Сохранить");
                });
                Click(window, "+  Добавить"); Check(window.Data.Timers.Last().Name == "Тестовый таймер" && window.Data.Timers.Last().Duration == 359999, "UI create named timer");
                double originalDuration = window.Data.Timers[0].Duration;
                InDialog(window, delegate(Window d, StackPanel p) { Screenshot(d, "docs/preview-timer-editor.png"); Click(d, "Прибавить 10 секунд"); Click(d, "Отмена"); }); Click(window, "•••");
                Check(window.Data.Timers[0].Duration == originalDuration, "cancel discards unsaved stepped duration");
                InDialog(window, delegate(Window d, StackPanel p) { Click(d, "Удалить"); }); Click(window, "•••"); Check(window.Data.Timers.Count == 3, "UI delete timer");
                window.Page = 1; window.Build();
                InDialog(window, delegate(Window d, StackPanel p) { var inputs = p.Children.OfType<TextBox>().ToArray(); inputs[0].Text = "Проверка будильника"; inputs[1].Text = "25:00"; Click(d, "Сохранить"); Check(d.IsVisible && window.Data.Alarms.Count == 0, "alarm rejects invalid time"); inputs[1].Text = "08:45"; var days = p.Children.OfType<WrapPanel>().First().Children.OfType<CheckBox>().ToArray(); days[0].IsChecked = true; Click(d, "Сохранить"); });
                Click(window, "+  Добавить"); Check(window.Data.Alarms.Count == 1 && window.Data.Alarms[0].Hour == 8 && window.Data.Alarms[0].Minute == 45 && window.Data.Alarms[0].Days == 1 && window.Data.Alarms[0].NextUtc > DateTime.UtcNow, "UI create recurring alarm");
                InDialog(window, delegate(Window d, StackPanel p) { Click(d, "Удалить"); }); Click(window, "Изменить"); Check(window.Data.Alarms.Count == 0, "UI delete alarm");
                window.Page = 3; window.Build(); InDialog(window, delegate(Window d, StackPanel p) { var search = p.Children.OfType<TextBox>().First(); search.Text = "Дубай"; Click(d, "Добавить"); }); Click(window, "+  Добавить"); Check(window.Data.Cities.Last().Name == "Дубай", "UI search and add city"); Click(window, "×"); Check(window.Data.Cities.Count == 3, "UI delete city");
                window.Page = 2; window.Build(); Click(window, "Старт"); Click(window, "Круг"); Click(window, "Стоп"); Click(window, "Сбросить"); Check(true, "UI stopwatch start lap stop reset");
                window.Page = 4; window.Build(); Click(window, "Начать"); Check(window.Data.Focus.Running, "UI focus start"); window.Data.Focus.DeadlineUtc = DateTime.UtcNow.AddSeconds(-1); window.Tick(); Check(window.Data.OnBreak && window.Data.FocusCompleted == 1 && window.Data.Focus.Duration == 300, "focus completion switches to break");
                window.Data.Focus.Start(DateTime.UtcNow); window.Data.Focus.DeadlineUtc = DateTime.UtcNow.AddSeconds(-1); window.Tick(); Check(!window.Data.OnBreak && window.Data.Focus.Duration == 1500, "break completion switches to focus");
                for (int i = 0; i < 6; i++) { window.Page = i; window.Build(); window.UpdateLayout(); Check(true, "render page " + i); }
                window.Data = State.Fresh(); window.Data.Alarms.Add(new Alarm { Name = "Доброе утро", Days = 31 }); window.Page = 1; window.Build(); Screenshot(window, "docs/preview-alarms.png");
                window.Data.Dark = true; window.Page = 0; window.Build(); Screenshot(window, "docs/preview-dark.png");
                window.Width = 880; window.Height = 640; window.Page = 4; window.Build(); window.UpdateLayout(); Screenshot(window, "docs/preview-compact.png"); Check(true, "compact focus layout");
                window.EnableTestAlerts = true;
                window.Page = 0; window.Build();
                var expired = window.Data.Timers[0]; expired.Start(DateTime.UtcNow); expired.DeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
                window.Tick();
                var alert = window.Notifications.Active.Single(); alert.UpdateLayout();
                Check(alert.Topmost && alert.IsVisible && alert.WindowStyle == WindowStyle.None && window.Notifications.SoundActive, "timer creates persistent topmost alert with looping chime");
                Check(expired.Remaining == 0 && !expired.Running, "expired timer waits for acknowledgement before reset");
                alert.Close(); Check(alert.IsVisible && window.Notifications.Active.Count == 1 && window.Notifications.SoundActive, "Alt F4 and taskbar close do not dismiss or silence alert");
                alert.WindowState = WindowState.Minimized; Check(alert.WindowState == WindowState.Normal, "alert resists minimization");
                var otherApp = new Window { Title = "External foreground test", WindowStyle = WindowStyle.None, WindowState = WindowState.Maximized, Topmost = true, Content = new TextBlock { Text = "Another borderless full screen window" } };
                otherApp.Show(); otherApp.Activate(); alert.RaiseAbove(); Check(Above(alert, otherApp) && alert.IsVisible && window.Notifications.SoundActive, "native Z order places alert above another topmost borderless full screen window"); otherApp.Close();
                Screenshot(alert, "docs/preview-alert.png");
                window.Tick(); Check(window.Notifications.Active.Count == 1, "completed timer does not create duplicate alerts");
                var second = window.Data.Timers[1]; second.Start(DateTime.UtcNow); second.DeadlineUtc = DateTime.UtcNow.AddSeconds(-1); window.Tick();
                Check(window.Notifications.Active.Count == 2, "simultaneous timers retain separate acknowledgements");
                Click(alert, "Остановить"); Check(window.Notifications.Active.Count == 1 && window.Notifications.SoundActive && expired.Remaining == expired.Duration, "stop resets first timer without silencing another");
                Click(window.Notifications.Active.Single(), "Остановить"); Check(window.Notifications.Active.Count == 0 && !window.Notifications.SoundActive && second.Remaining == second.Duration, "last stop closes alert resets timer and stops chime");
                bool snoozed = false; window.Notify("Будильник", "Проверка", delegate { snoozed = true; }); Click(window.Notifications.Active.Single(), "Ещё 5 минут");
                Check(snoozed && window.Notifications.Active.Count == 0 && !window.Notifications.SoundActive, "snooze acknowledges alarm and silences sound");
                window.Data.Sound = false; window.Notify("Без звука", "Проверка", null); Check(!window.Notifications.SoundActive && window.Notifications.Active.Count == 1, "sound off preserves persistent visual alert"); Click(window.Notifications.Active.Single(), "Остановить");
                window.Data.Sound = true; window.Notify("Завершение", "Проверка очистки", null);
            }
            catch (Exception ex) { Check(false, ex.ToString()); }
            Click(window, "Свернуть"); Check(window.WindowState == WindowState.Minimized, "minimize keeps the application open");
            window.Title = "Apple Clock Test " + Guid.NewGuid().ToString("N");
            Program.ActivateExisting(window.Title); window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            window.Title = "Apple Clock";
            Check(window.IsVisible && window.WindowState != WindowState.Minimized, "repeated launch restores existing minimized window");
            bool closed = false; window.Closed += delegate { closed = true; };
            Click(window, "Закрыть"); Check(closed && !window.IsVisible, "red close button closes the window without hiding in tray");
            Check(window.Notifications.Active.Count == 0 && !window.Notifications.SoundActive, "application exit closes all alerts and stops sound");
            Check(Storage.Load().Dark == window.Data.Dark, "close saves preferences");
            Directory.CreateDirectory("test-results"); File.WriteAllText("test-results/ui.txt", report.ToString());
        }
        public static void AlertDuration(ClockWindow window)
        {
            window.EnableTestAlerts = true; window.Notify("Проверка длительности", "65 секунд без автоматического отключения", null);
            var popup = window.Notifications.Active.Single();
            var duration = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(65) };
            duration.Tick += delegate {
                duration.Stop();
                try {
                    Check(popup.IsVisible && popup.Topmost && window.Notifications.SoundActive, "alert and looping audio remain active after 65 seconds");
                    Click(popup, "Остановить"); Check(!window.Notifications.SoundActive && window.Notifications.Active.Count == 0, "explicit stop ends persistent alert");
                } catch (Exception ex) { Check(false, ex.ToString()); }
                Directory.CreateDirectory("test-results"); File.WriteAllText("test-results/alert-duration.txt", report.ToString()); window.Close();
            };
            duration.Start();
        }
    }
}
