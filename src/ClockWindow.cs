using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace AppleClock
{
    public class Dial : FrameworkElement
    {
        public double Progress = 1;
        public Brush Accent = Brushes.Orange, Track = Brushes.LightGray;
        protected override void OnRender(DrawingContext dc)
        {
            double r = Math.Max(1, Math.Min(ActualWidth, ActualHeight) / 2 - 10); Point c = new Point(ActualWidth / 2, ActualHeight / 2);
            dc.DrawEllipse(null, new Pen(Track, 7), c, r, r);
            double p = Math.Max(0, Math.Min(1, Progress)); if (p <= 0) return;
            if (p >= .99999) { dc.DrawEllipse(null, new Pen(Accent, 7), c, r, r); return; }
            double angle = p * Math.PI * 2; var geometry = new StreamGeometry();
            using (var context = geometry.Open()) { context.BeginFigure(new Point(c.X, c.Y - r), false, false); context.ArcTo(new Point(c.X + Math.Sin(angle) * r, c.Y - Math.Cos(angle) * r), new Size(r, r), 0, p > .5, SweepDirection.Clockwise, true, false); }
            dc.DrawGeometry(null, new Pen(Accent, 7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, geometry);
        }
    }
    public class ClockWindow : Window
    {
        public State Data;
        public int Page;
        public bool Testing, Quitting;
        Grid shell;
        StackPanel navigation, body;
        TextBlock title, subtitle, localTime;
        readonly List<Action> refresh = new List<Action>();
        readonly Stopwatch stopwatch = new Stopwatch();
        readonly List<TimeSpan> laps = new List<TimeSpan>();
        readonly DispatcherTimer ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        Forms.NotifyIcon tray;
        DateTime lastSave = DateTime.UtcNow;
        Brush bg, side, card, ink, secondary, line;
        readonly Brush orange = new SolidColorBrush(Color.FromRgb(255, 149, 0));
        readonly Brush blue = new SolidColorBrush(Color.FromRgb(0, 122, 255));
        readonly string[] pages = { "Таймеры", "Будильники", "Секундомер", "Мировые часы", "Фокусировка", "Настройки" };
        readonly string[] iconPaths = {
            "M4,2 L16,2 M4,18 L16,18 M5,2 C5,8 8,8 10,10 C12,12 15,12 15,18 M15,2 C15,8 12,8 10,10 C8,12 5,12 5,18",
            "M10,4 C1,4 1,17 10,17 C19,17 19,4 10,4 M10,7 L10,11 13,13 M3,2 L1,5 M17,2 L19,5 M5,17 L3,19 M15,17 L17,19",
            "M10,5 C1,5 1,19 10,19 C19,19 19,5 10,5 M7,1 L13,1 M10,1 L10,5 M10,8 L10,12 13,12 M16,5 L18,3",
            "M10,1 C-2,1 -2,19 10,19 C22,19 22,1 10,1 M10,1 C4,5 4,15 10,19 M10,1 C16,5 16,15 10,19 M1,10 L19,10 M3,5 L17,5 M3,15 L17,15",
            "M10,1 C-2,1 -2,19 10,19 C22,19 22,1 10,1 M10,5 C3,5 3,15 10,15 C17,15 17,5 10,5 M10,8 L10,12 M8,10 L12,10",
            "M8,1 L12,1 13,4 16,4 19,8 17,10 19,13 16,17 13,16 12,19 8,19 7,16 4,17 1,13 3,10 1,8 4,4 7,4 Z M10,6 C4,6 4,14 10,14 C16,14 16,6 10,6" };
        public ClockWindow(State data, bool testing)
        {
            Data = data; Testing = testing; Title = "Apple Clock"; Width = 1180; Height = 790; MinWidth = 880; MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
            var chrome = new System.Windows.Shell.WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), CornerRadius = new CornerRadius(12), GlassFrameThickness = new Thickness(0) };
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("AppleClock.Icon")) Icon = System.Windows.Media.Imaging.BitmapFrame.Create(stream, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            Build();
            if (!testing) SetupTray();
            ticker.Tick += delegate { Tick(); }; ticker.Start();
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!Quitting && Data.Background && !Testing) { e.Cancel = true; Hide(); tray.ShowBalloonTip(2500, "Apple Clock", "Часы работают в фоне. Открыть или завершить — через значок в трее.", Forms.ToolTipIcon.Info); } Save(); };
            Closed += delegate { ticker.Stop(); if (tray != null) tray.Dispose(); };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape && WindowState == WindowState.Maximized) WindowState = WindowState.Normal; if (e.Key == Key.F11) Maximize(); };
        }
        Brush ColorBrush(string value) { return (Brush)new BrushConverter().ConvertFromString(value); }
        TextBlock Text(string value, double size, Brush color) { return new TextBlock { Text = value, FontSize = size, Foreground = color, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }; }
        Button Button(string value, Action action, bool primary = false)
        {
            var b = new Button { Content = value, Padding = new Thickness(16, 9, 16, 9), Margin = new Thickness(0, 0, 8, 0), Background = primary ? blue : line, Foreground = primary ? Brushes.White : ink, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, MinHeight = 36 };
            var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10)); border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(FrameworkElement.MarginProperty, new Thickness(14, 9, 14, 9)); content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
            b.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            b.Click += delegate { action(); }; b.MouseEnter += delegate { b.Opacity = .75; }; b.MouseLeave += delegate { b.Opacity = 1; };
            System.Windows.Automation.AutomationProperties.SetName(b, value); return b;
        }
        Border Card(UIElement child, Thickness padding) { return new Border { Child = child, Background = card, CornerRadius = new CornerRadius(18), Padding = padding, Margin = new Thickness(0, 0, 18, 18), BorderBrush = line, BorderThickness = new Thickness(1) }; }
        public void Build()
        {
            bg = ColorBrush(Data.Dark ? "#1C1C1E" : "#F5F5F7"); side = ColorBrush(Data.Dark ? "#262628" : "#ECECEF"); card = ColorBrush(Data.Dark ? "#2C2C2E" : "#FFFFFF"); ink = ColorBrush(Data.Dark ? "#F5F5F7" : "#1D1D1F"); secondary = ColorBrush(Data.Dark ? "#A1A1A6" : "#86868B"); line = ColorBrush(Data.Dark ? "#3A3A3C" : "#E9E9ED");
            Background = bg; Foreground = ink;
            shell = new Grid { Background = bg }; shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(224) }); shell.ColumnDefinitions.Add(new ColumnDefinition());
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) }); shell.RowDefinitions.Add(new RowDefinition());
            var sidebar = new Border { Background = side }; Grid.SetRowSpan(sidebar, 2); shell.Children.Add(sidebar);
            var traffic = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(22, 19, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            string[] colors = { "#FF5F57", "#FEBC2E", "#28C840" }; string[] labels = { "Закрыть", "Свернуть", "Развернуть" };
            for (int i = 0; i < 3; i++) { int n = i; var dot = new Button { Width = 13, Height = 13, Margin = new Thickness(0, 0, 9, 0), Background = ColorBrush(colors[i]), BorderThickness = new Thickness(0), ToolTip = labels[i] }; var factory = new FrameworkElementFactory(typeof(Border)); factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(7)); factory.SetValue(Border.BackgroundProperty, ColorBrush(colors[i])); dot.Template = new ControlTemplate(typeof(Button)) { VisualTree = factory }; dot.Click += delegate { if (n == 0) Close(); else if (n == 1) WindowState = WindowState.Minimized; else Maximize(); }; System.Windows.Automation.AutomationProperties.SetName(dot, labels[i]); traffic.Children.Add(dot); }
            shell.Children.Add(traffic);
            var sideContent = new DockPanel { Margin = new Thickness(14, 14, 14, 20) }; Grid.SetRow(sideContent, 1); shell.Children.Add(sideContent);
            var bottom = new StackPanel { Margin = new Thickness(10, 0, 0, 0) }; bottom.Children.Add(Text("APPLE CLOCK", 11, secondary)); localTime = Text("", 28, ink); bottom.Children.Add(localTime); bottom.Children.Add(Text("Время для важного.", 12, secondary)); DockPanel.SetDock(bottom, Dock.Bottom); sideContent.Children.Add(bottom);
            navigation = new StackPanel(); navigation.Children.Add(Text("   Часы", 25, ink)); navigation.Children.Add(new Border { Height = 25 });
            for (int i = 0; i < pages.Length; i++)
            {
                int n = i; Button b = Button(pages[i], delegate { Page = n; Build(); }); b.Background = Brushes.Transparent; b.Margin = new Thickness(0, 3, 0, 3);
                if (Page == i) { b.Background = blue; b.Foreground = Brushes.White; }
                var label = new StackPanel { Orientation = Orientation.Horizontal, Width = 166 };
                label.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse(iconPaths[i]), Stroke = b.Foreground, StrokeThickness = 1.35, Width = 18, Height = 18, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
                label.Children.Add(Text(pages[i], 13, b.Foreground)); b.Content = label; navigation.Children.Add(b);
            }
            sideContent.Children.Add(navigation);
            var bar = new Grid { Background = bg, Margin = new Thickness(28, 0, 24, 0) }; Grid.SetColumn(bar, 1); shell.Children.Add(bar);
            var breadcrumb = Text("Часы  /  " + pages[Page], 13, secondary); bar.Children.Add(breadcrumb); bar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) Maximize(); else DragMove(); };
            var theme = Button(Data.Dark ? "☀  Светлая" : "◐  Тёмная", delegate { Data.Dark = !Data.Dark; Save(); Build(); }); theme.HorizontalAlignment = HorizontalAlignment.Right; theme.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(theme);
            var main = new DockPanel { Margin = new Thickness(32, 12, 14, 18) }; Grid.SetRow(main, 1); Grid.SetColumn(main, 1); shell.Children.Add(main);
            var header = new Grid { Margin = new Thickness(0, 0, 18, 28) }; header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var headings = new StackPanel(); title = Text(pages[Page], 34, ink); title.FontWeight = FontWeights.SemiBold; headings.Children.Add(title); subtitle = Text(new[] { "Маленькие паузы. Большие планы.", "Начните день в своём ритме.", "Каждое мгновение имеет значение.", "Один момент. Разные города.", "Одно дело. Всё внимание.", "Ваши часы — ваши привычки." }[Page], 14, secondary); subtitle.Margin = new Thickness(0, 8, 0, 0); headings.Children.Add(subtitle); header.Children.Add(headings);
            if (Page == 0 || Page == 1 || Page == 3) { var add = Button("+  Добавить", delegate { if (Page == 0) EditTimer(null); else if (Page == 1) EditAlarm(null); else AddCity(); }, true); add.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(add, 1); header.Children.Add(add); }
            DockPanel.SetDock(header, Dock.Top); main.Children.Add(header);
            body = new StackPanel(); main.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            Content = shell; refresh.Clear();
            if (Page == 0) Timers(); else if (Page == 1) Alarms(); else if (Page == 2) StopwatchPage(); else if (Page == 3) World(); else if (Page == 4) FocusPage(); else Settings();
            Refresh();
        }
        void Maximize() { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
        void Save() { if (!Storage.Save(Data) && !Testing) subtitle.Text = Storage.LastError; }
        void SetupTray()
        {
            tray = new Forms.NotifyIcon { Text = "Apple Clock", Visible = true };
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("AppleClock.Icon")) tray.Icon = new System.Drawing.Icon(stream);
            var menu = new Forms.ContextMenuStrip(); menu.Items.Add("Открыть часы", null, delegate { ShowWindow(); }); menu.Items.Add("Завершить", null, delegate { Quitting = true; Close(); }); tray.ContextMenuStrip = menu; tray.DoubleClick += delegate { ShowWindow(); };
        }
        void ShowWindow() { Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); }
        public void Tick()
        {
            DateTime now = DateTime.UtcNow; bool changed = false;
            foreach (Countdown t in Data.Timers.ToArray()) if (t.Tick(now)) { Notify(t.Name, "Время вышло", null); changed = true; }
            foreach (Alarm a in Data.Alarms.ToArray()) if (a.Tick(now)) { Alarm captured = a; Notify(a.Name, a.Hour.ToString("00") + ":" + a.Minute.ToString("00"), delegate { captured.Enabled = true; captured.NextUtc = DateTime.UtcNow.AddMinutes(5); Save(); Build(); }); changed = true; }
            string day = DateTime.Now.ToString("yyyy-MM-dd"); if (Data.FocusDay != day) { Data.FocusDay = day; Data.FocusCompleted = 0; changed = true; }
            if (Data.Focus.Tick(now)) { bool wasBreak = Data.OnBreak; if (!wasBreak) Data.FocusCompleted++; Data.OnBreak = !wasBreak; Data.Focus.Duration = (Data.OnBreak ? Data.BreakMinutes : Data.FocusMinutes) * 60; Data.Focus.Reset(); Notify("Фокусировка", wasBreak ? "Перерыв завершён. Можно вернуться к делу." : "Отличная работа. Время сделать перерыв.", null); changed = true; }
            Refresh(); if (changed || now - lastSave > TimeSpan.FromSeconds(15)) { Save(); lastSave = now; }
        }
        void Refresh() { if (localTime != null) localTime.Text = DateTime.Now.ToString("HH:mm"); foreach (Action action in refresh.ToArray()) action(); }
        public static string Duration(double seconds) { var value = TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, seconds))); return ((int)value.TotalHours).ToString("00") + ":" + value.Minutes.ToString("00") + ":" + value.Seconds.ToString("00"); }
        public static bool ParseDuration(string text, out double seconds)
        {
            seconds = 0; string[] parts = text.Trim().Split(':'); int h, m, s;
            if (parts.Length != 3 || parts.Any(p => p.Length != 2 || p.Any(c => c < '0' || c > '9')) || !int.TryParse(parts[0], out h) || !int.TryParse(parts[1], out m) || !int.TryParse(parts[2], out s) || h > 99 || m > 59 || s > 59) return false;
            seconds = h * 3600 + m * 60 + s; return seconds > 0;
        }
        void Timers()
        {
            var quick = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 24) }; quick.Children.Add(Text("Быстрый старт   ", 13, secondary));
            foreach (int minutes in new[] { 1, 5, 10, 15, 30 }) { int m = minutes; quick.Children.Add(Button(m + " мин", delegate { var t = new Countdown { Name = m + " минут", Duration = m * 60, Remaining = m * 60 }; Data.Timers.Add(t); t.Start(DateTime.UtcNow); Save(); Build(); })); } body.Children.Add(quick);
            var cards = new WrapPanel(); body.Children.Add(cards);
            foreach (Countdown t in Data.Timers)
            {
                var stack = new StackPanel { Width = 292 }; var top = new DockPanel(); var edit = Button("•••", delegate { EditTimer(t); }); edit.ToolTip = "Изменить таймер"; DockPanel.SetDock(edit, Dock.Right); top.Children.Add(edit); var name = Text(t.Name, 17, ink); name.FontWeight = FontWeights.SemiBold; top.Children.Add(name); stack.Children.Add(top);
                var ringGrid = new Grid { Width = 242, Height = 242, Margin = new Thickness(0, 20, 0, 20) }; var ring = new Dial { Accent = orange, Track = line }; ringGrid.Children.Add(ring);
                var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; var time = Text("", 38, ink); time.FontWeight = FontWeights.Light; time.HorizontalAlignment = HorizontalAlignment.Center; center.Children.Add(time); var status = Text("", 12, secondary); status.HorizontalAlignment = HorizontalAlignment.Center; status.Margin = new Thickness(0, 8, 0, 0); center.Children.Add(status); ringGrid.Children.Add(center); stack.Children.Add(ringGrid);
                var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center }; controls.Children.Add(Button("Сбросить", delegate { t.Reset(); Save(); Refresh(); })); var toggle = Button("", delegate { if (t.Running) t.Pause(DateTime.UtcNow); else t.Start(DateTime.UtcNow); Save(); Refresh(); }, true); controls.Children.Add(toggle); stack.Children.Add(controls);
                refresh.Add(delegate { time.Text = Duration(t.Remaining); status.Text = t.Running ? "Осталось" : (t.Remaining <= 0 ? "Время вышло" : "Готов к запуску"); toggle.Content = t.Running ? "Пауза" : "Старт"; ring.Progress = t.Remaining / t.Duration; ring.InvalidateVisual(); }); cards.Children.Add(Card(stack, new Thickness(24)));
            }
            if (Data.Timers.Count == 0) body.Children.Add(Text("Добавьте таймер или выберите время быстрого старта.", 16, secondary));
            body.Children.Add(Text("Таймер продолжит отсчёт, пока вы занимаетесь своими делами.", 12, secondary));
        }
        void Alarms()
        {
            if (Data.Alarms.Count == 0) { body.Children.Add(Text("Пока всё спокойно", 25, ink)); body.Children.Add(Text("Добавьте первый будильник и выберите дни повторения.", 15, secondary)); }
            foreach (Alarm a in Data.Alarms.OrderBy(x => x.Hour * 60 + x.Minute))
            {
                var row = new DockPanel(); var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; var enabled = new CheckBox { Content = "Включён", IsChecked = a.Enabled, Foreground = ink, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 20, 0) }; enabled.Checked += delegate { a.Enabled = true; a.Schedule(DateTime.Now); Save(); }; enabled.Unchecked += delegate { a.Enabled = false; Save(); }; actions.Children.Add(enabled); actions.Children.Add(Button("Изменить", delegate { EditAlarm(a); })); DockPanel.SetDock(actions, Dock.Right); row.Children.Add(actions);
                var info = new StackPanel(); var time = Text(a.Hour.ToString("00") + ":" + a.Minute.ToString("00"), 48, a.Enabled ? ink : secondary); time.FontWeight = FontWeights.Light; info.Children.Add(time); info.Children.Add(Text(a.Name + " · " + DaysText(a.Days), 14, secondary)); row.Children.Add(info); body.Children.Add(Card(row, new Thickness(24, 18, 20, 18)));
            }
            body.Children.Add(Text("Будильники звучат, когда приложение открыто или работает в трее. Компьютер должен бодрствовать.", 12, secondary));
        }
        string DaysText(int days) { if (days == 0) return "Один раз"; if (days == 127) return "Каждый день"; string[] names = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" }; return string.Join(", ", names.Where((s, i) => (days & (1 << i)) != 0)); }
        void StopwatchPage()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 35, 0, 0) }; var time = Text("", 82, ink); time.FontWeight = FontWeights.Light; time.HorizontalAlignment = HorizontalAlignment.Center; panel.Children.Add(time);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 32, 0, 32) };
            var lap = Button("Круг", delegate { if (stopwatch.IsRunning) { laps.Add(stopwatch.Elapsed); Build(); } }); buttons.Children.Add(lap); var start = Button("", delegate { if (stopwatch.IsRunning) stopwatch.Stop(); else stopwatch.Start(); Refresh(); }, true); buttons.Children.Add(start); buttons.Children.Add(Button("Сбросить", delegate { stopwatch.Reset(); laps.Clear(); Build(); })); panel.Children.Add(buttons);
            for (int i = laps.Count - 1; i >= 0; i--) { var diff = laps[i] - (i == 0 ? TimeSpan.Zero : laps[i - 1]); var row = new DockPanel(); var total = Text(Elapsed(laps[i]), 17, secondary); DockPanel.SetDock(total, Dock.Right); row.Children.Add(total); row.Children.Add(Text("Круг " + (i + 1) + "     " + Elapsed(diff), 17, ink)); panel.Children.Add(Card(row, new Thickness(20, 12, 20, 12))); }
            refresh.Add(delegate { time.Text = Elapsed(stopwatch.Elapsed); start.Content = stopwatch.IsRunning ? "Стоп" : "Старт"; lap.IsEnabled = stopwatch.IsRunning; }); body.Children.Add(panel);
        }
        static string Elapsed(TimeSpan time) { return ((int)time.TotalMinutes).ToString("00") + ":" + time.Seconds.ToString("00") + "." + (time.Milliseconds / 10).ToString("00"); }
        void World()
        {
            foreach (City c in Data.Cities.ToArray())
            {
                var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); var info = new StackPanel(); info.Children.Add(Text(c.Name, 25, ink)); var date = Text("", 13, secondary); info.Children.Add(date); row.Children.Add(info); var time = Text("", 48, ink); time.FontWeight = FontWeights.Light; time.Margin = new Thickness(20, 0, 26, 0); Grid.SetColumn(time, 1); row.Children.Add(time); var delete = Button("×", delegate { Data.Cities.Remove(c); Save(); Build(); }); delete.ToolTip = "Удалить город"; delete.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(delete, 2); row.Children.Add(delete);
                refresh.Add(delegate { DateTime at = c.At(DateTime.UtcNow); time.Text = at.ToString("HH:mm:ss"); date.Text = at.ToString("ddd, d MMMM", CultureInfo.GetCultureInfo("ru-RU")) + " · " + (at.Hour >= 6 && at.Hour < 22 ? "День" : "Ночь"); }); body.Children.Add(Card(row, new Thickness(26, 22, 16, 22)));
            }
            body.Children.Add(Text("Время городов учитывает часовые пояса и переход на летнее время.", 12, secondary));
        }
        void FocusPage()
        {
            var panel = new StackPanel(); panel.Children.Add(Text("Над чем вы работаете?", 13, secondary)); var task = new TextBox { Text = Data.FocusTask, FontSize = 17, Padding = new Thickness(14), Margin = new Thickness(0, 8, 0, 0), Background = card, Foreground = ink, BorderBrush = line, MaxLength = 160 }; task.ToolTip = "Над чем вы работаете?"; task.TextChanged += delegate { Data.FocusTask = task.Text; }; panel.Children.Add(task);
            var grid = new Grid { Width = 290, Height = 290, Margin = new Thickness(0, 26, 0, 22) }; var ring = new Dial { Accent = blue, Track = line }; grid.Children.Add(ring); var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; var caption = Text("", 15, secondary); caption.HorizontalAlignment = HorizontalAlignment.Center; center.Children.Add(caption); var time = Text("", 51, ink); time.HorizontalAlignment = HorizontalAlignment.Center; center.Children.Add(time); grid.Children.Add(center); panel.Children.Add(grid);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center }; buttons.Children.Add(Button("Сбросить", delegate { Data.Focus.Reset(); Save(); Refresh(); })); var toggle = Button("", delegate { if (Data.Focus.Running) Data.Focus.Pause(DateTime.UtcNow); else Data.Focus.Start(DateTime.UtcNow); Save(); Refresh(); }, true); buttons.Children.Add(toggle); panel.Children.Add(buttons);
            var completed = Text("", 15, secondary); completed.HorizontalAlignment = HorizontalAlignment.Center; completed.Margin = new Thickness(0, 22, 0, 26); panel.Children.Add(completed);
            var intervals = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center }; intervals.Children.Add(Text("Фокус, мин  ", 14, secondary)); var work = NumberBox(Data.FocusMinutes); intervals.Children.Add(work); intervals.Children.Add(Text("    Перерыв, мин  ", 14, secondary)); var rest = NumberBox(Data.BreakMinutes); intervals.Children.Add(rest); intervals.Children.Add(Button("Применить", delegate { int w, r; if (!int.TryParse(work.Text, out w) || !int.TryParse(rest.Text, out r) || w < 1 || w > 180 || r < 1 || r > 60) { MessageBox.Show(this, "Фокус: 1–180 минут. Перерыв: 1–60 минут."); return; } Data.FocusMinutes = w; Data.BreakMinutes = r; Data.Focus.Duration = (Data.OnBreak ? r : w) * 60; Data.Focus.Reset(); Save(); Refresh(); })); panel.Children.Add(intervals);
            refresh.Add(delegate { caption.Text = Data.OnBreak ? "Перерыв" : "Время сосредоточиться"; time.Text = Duration(Data.Focus.Remaining); toggle.Content = Data.Focus.Running ? "Пауза" : "Начать"; ring.Progress = Data.Focus.Remaining / Data.Focus.Duration; ring.InvalidateVisual(); completed.Text = "Завершено сегодня: " + Data.FocusCompleted + " сессий"; }); body.Children.Add(Card(panel, new Thickness(28)));
        }
        void Settings()
        {
            var panel = new StackPanel(); panel.Children.Add(Text("Оформление и работа", 21, ink)); panel.Children.Add(new Border { Height = 18 });
            AddCheck(panel, "Тёмная тема", Data.Dark, delegate(bool value) { Data.Dark = value; Save(); Build(); });
            AddCheck(panel, "Звуковой сигнал таймеров и будильников", Data.Sound, delegate(bool value) { Data.Sound = value; Save(); });
            AddCheck(panel, "Продолжать работу в трее после закрытия окна", Data.Background, delegate(bool value) { Data.Background = value; Save(); });
            panel.Children.Add(Text("После сна истёкший таймер сработает при пробуждении. Часы не включают компьютер и не будят его. Будильник с опозданием более 10 минут пропускается. Секундомер работает до завершения приложения.", 13, secondary)); panel.Children.Add(new Border { Height = 20 }); panel.Children.Add(Button("Проверить звук", delegate { Notify("Apple Clock", "Так звучит ваш сигнал.", null); })); panel.Children.Add(new Border { Height = 20 }); panel.Children.Add(Button("Завершить приложение", delegate { Quitting = true; Close(); })); body.Children.Add(Card(panel, new Thickness(26)));
            body.Children.Add(Text("Apple Clock 1.0.0 · Apple Design\nСамостоятельное приложение для Windows. Не связано с Apple или Microsoft.\nНастройки хранятся только на вашем компьютере.", 13, secondary));
        }
        void AddCheck(Panel parent, string label, bool value, Action<bool> change) { var check = new CheckBox { Content = label, IsChecked = value, Foreground = ink, FontSize = 15, Margin = new Thickness(0, 0, 0, 22) }; check.Checked += delegate { change(true); }; check.Unchecked += delegate { change(false); }; parent.Children.Add(check); }
        TextBox NumberBox(int value) { return new TextBox { Text = value.ToString(), Width = 60, MaxLength = 3, FontSize = 17, Padding = new Thickness(7), Background = card, Foreground = ink, BorderBrush = line, Margin = new Thickness(0, 0, 10, 0), VerticalContentAlignment = VerticalAlignment.Center }; }
        Window Dialog(string name, out StackPanel fields)
        {
            var dialog = new Window { Title = name, Owner = this, Width = 460, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = bg, Foreground = ink, FontFamily = FontFamily, ShowInTaskbar = false };
            fields = new StackPanel { Margin = new Thickness(28) }; fields.Children.Add(Text(name, 25, ink)); fields.Children.Add(new Border { Height = 22 }); dialog.Content = fields; return dialog;
        }
        TextBox Field(Panel panel, string caption, string value) { panel.Children.Add(Text(caption, 13, secondary)); var input = new TextBox { Text = value, Padding = new Thickness(10), Margin = new Thickness(0, 6, 0, 18), FontSize = 17, Background = card, Foreground = ink, BorderBrush = line, MaxLength = 100 }; panel.Children.Add(input); return input; }
        void EditTimer(Countdown timer)
        {
            StackPanel fields; var dialog = Dialog(timer == null ? "Новый таймер" : "Изменить таймер", out fields); var name = Field(fields, "Название", timer == null ? "Таймер" : timer.Name); var value = Field(fields, "Время · ЧЧ:ММ:СС", Duration(timer == null ? 300 : timer.Duration)); var error = Text("", 12, Brushes.IndianRed); fields.Children.Add(error);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal }; buttons.Children.Add(Button("Сохранить", delegate { double duration; if (!ParseDuration(value.Text, out duration)) { error.Text = "Введите время от 00:00:01 до 99:59:59."; return; } if (timer == null) { timer = new Countdown(); Data.Timers.Add(timer); } timer.Name = string.IsNullOrWhiteSpace(name.Text) ? "Таймер" : name.Text.Trim(); timer.Duration = duration; timer.Reset(); Save(); dialog.Close(); Build(); }, true));
            if (timer != null) buttons.Children.Add(Button("Удалить", delegate { Data.Timers.Remove(timer); Save(); dialog.Close(); Build(); })); buttons.Children.Add(Button("Отмена", delegate { dialog.Close(); })); fields.Children.Add(buttons); dialog.ShowDialog();
        }
        void EditAlarm(Alarm alarm)
        {
            StackPanel fields; var dialog = Dialog(alarm == null ? "Новый будильник" : "Изменить будильник", out fields); var name = Field(fields, "Название", alarm == null ? "Будильник" : alarm.Name); var time = Field(fields, "Время · ЧЧ:ММ", alarm == null ? "07:30" : alarm.Hour.ToString("00") + ":" + alarm.Minute.ToString("00")); fields.Children.Add(Text("Повторять · без отметок — один раз", 13, secondary)); var days = new WrapPanel { Margin = new Thickness(0, 15, 0, 20) }; var checks = new List<CheckBox>(); var labels = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" }; for (int i = 0; i < 7; i++) { var c = new CheckBox { Content = labels[i], IsChecked = alarm != null && (alarm.Days & (1 << i)) != 0, Foreground = ink, Margin = new Thickness(0, 0, 12, 14) }; checks.Add(c); days.Children.Add(c); } fields.Children.Add(days); var error = Text("", 12, Brushes.IndianRed); fields.Children.Add(error);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal }; buttons.Children.Add(Button("Сохранить", delegate { DateTime parsed; if (!DateTime.TryParseExact(time.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) { error.Text = "Введите время от 00:00 до 23:59."; return; } if (alarm == null) { alarm = new Alarm(); Data.Alarms.Add(alarm); } alarm.Name = string.IsNullOrWhiteSpace(name.Text) ? "Будильник" : name.Text.Trim(); alarm.Hour = parsed.Hour; alarm.Minute = parsed.Minute; alarm.Days = 0; for (int i = 0; i < 7; i++) if (checks[i].IsChecked == true) alarm.Days |= 1 << i; alarm.Enabled = true; alarm.Schedule(DateTime.Now); Save(); dialog.Close(); Build(); }, true)); if (alarm != null) buttons.Children.Add(Button("Удалить", delegate { Data.Alarms.Remove(alarm); Save(); dialog.Close(); Build(); })); buttons.Children.Add(Button("Отмена", delegate { dialog.Close(); })); fields.Children.Add(buttons); dialog.ShowDialog();
        }
        void AddCity()
        {
            StackPanel fields; var dialog = Dialog("Добавить город", out fields); var search = Field(fields, "Поиск города", ""); var list = new ListBox { Height = 270, Background = card, Foreground = ink, BorderBrush = line, FontSize = 18, Margin = new Thickness(0, 0, 0, 20), DisplayMemberPath = "" }; Action populate = delegate { list.Items.Clear(); foreach (City c in City.Catalog.Where(x => x.Name.IndexOf(search.Text, StringComparison.CurrentCultureIgnoreCase) >= 0 && !Data.Cities.Any(y => y.Zone == x.Zone))) list.Items.Add(c.Name); if (list.Items.Count > 0) list.SelectedIndex = 0; }; search.TextChanged += delegate { populate(); }; populate(); fields.Children.Add(list); fields.Children.Add(Button("Добавить", delegate { if (list.SelectedItem == null) return; Data.Cities.Add(City.Catalog.First(x => x.Name == (string)list.SelectedItem)); Save(); dialog.Close(); Build(); }, true)); dialog.ShowDialog();
        }
        void Notify(string name, string message, Action snooze)
        {
            if (Testing) return;
            var dialog = new Window { Title = "Apple Clock · " + name, Width = 420, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true, Background = bg, Foreground = ink, ResizeMode = ResizeMode.NoResize };
            var panel = new StackPanel { Margin = new Thickness(28) }; panel.Children.Add(Text(name, 27, ink)); panel.Children.Add(new Border { Height = 12 }); panel.Children.Add(Text(message, 17, secondary)); panel.Children.Add(new Border { Height = 26 }); var buttons = new StackPanel { Orientation = Orientation.Horizontal }; buttons.Children.Add(Button("Остановить", delegate { dialog.Close(); }, true)); if (snooze != null) buttons.Children.Add(Button("Ещё 5 минут", delegate { snooze(); dialog.Close(); })); panel.Children.Add(buttons); dialog.Content = panel;
            var sound = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; int count = 0; Action play = delegate { if (Data.Sound) System.Media.SystemSounds.Exclamation.Play(); }; play(); sound.Tick += delegate { play(); if (++count >= 30) sound.Stop(); }; sound.Start(); dialog.Closed += delegate { sound.Stop(); }; dialog.Show();
        }
    }
}
