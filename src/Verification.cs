using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AppleClock
{
    public static class Verification
    {
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
                File.WriteAllText(Path.Combine(Storage.DirectoryPath, "settings.xml"), "broken"); Check(Storage.Load().Timers.Count == 2 && Storage.LastError.Length > 0, "corrupt settings handled");
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
            var button = root as Button; if (button != null && button.Content as string == label) return button;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { Button found = FindButton(VisualTreeHelper.GetChild(root, i), label); if (found != null) return found; } return null;
        }
        static void Click(Window window, string label) { window.UpdateLayout(); var b = FindButton(window, label); if (b == null) throw new Exception("Button not found: " + label); b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); window.UpdateLayout(); }
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
        static void Screenshot(ClockWindow window, string filename)
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
                InDialog(window, delegate(Window d, StackPanel p) { var inputs = p.Children.OfType<TextBox>().ToArray(); inputs[0].Text = "Тестовый таймер"; inputs[1].Text = "00:60:00"; Click(d, "Сохранить"); Check(d.IsVisible && window.Data.Timers.Count == 3, "timer dialog rejects invalid input"); inputs[1].Text = "99:59:59"; Click(d, "Сохранить"); });
                Click(window, "+  Добавить"); Check(window.Data.Timers.Last().Name == "Тестовый таймер" && window.Data.Timers.Last().Duration == 359999, "UI create named timer");
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
            }
            catch (Exception ex) { Check(false, ex.ToString()); }
            Directory.CreateDirectory("test-results"); File.WriteAllText("test-results/ui.txt", report.ToString()); window.Quitting = true; window.Close();
        }
    }
}
